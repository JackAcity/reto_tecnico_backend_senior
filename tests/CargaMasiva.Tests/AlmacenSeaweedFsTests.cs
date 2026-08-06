using System.Net;
using Almacenamiento;

namespace CargaMasiva.Tests;

/// <summary>
/// El nombre de archivo lo elige el usuario — frecuente en español con tildes y
/// espacios ("Catálogo Q1.xlsx"). Sin escapar, esos caracteres rompen la URI que
/// se arma para el filer de SeaweedFS o la truncan en un punto inesperado.
/// Unitario: intercepta el HttpClient, no necesita SeaweedFS real.
/// </summary>
public class AlmacenSeaweedFsTests
{
    private sealed class HandlerEspia : HttpMessageHandler
    {
        public Uri? UriCapturada { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UriCapturada = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }
    }

    private static (AlmacenSeaweedFs Almacen, HandlerEspia Espia) Crear()
    {
        var espia = new HandlerEspia();
        var http = new HttpClient(espia) { BaseAddress = new Uri("http://seaweedfs:8888/") };
        return (new AlmacenSeaweedFs(http), espia);
    }

    [Theory]
    [InlineData("Catálogo Q1.xlsx")]
    [InlineData("reporte final (v2).xlsx")]
    [InlineData("2025 - carga #1.xlsx")]
    public async Task NombreConCaracteresEspeciales_NoRompeLaUri(string nombre)
    {
        var (almacen, espia) = Crear();

        var ruta = await almacen.SubirAsync(new MemoryStream([1, 2, 3]), nombre, CancellationToken.None);

        Assert.NotNull(espia.UriCapturada);
        Assert.StartsWith("seaweed://cargas/", ruta);
    }

    [Fact]
    public async Task NombreConEspacio_ViajaEscapadoEnLaUri()
    {
        var (almacen, espia) = Crear();

        await almacen.SubirAsync(new MemoryStream([1, 2, 3]), "reporte final.xlsx", CancellationToken.None);

        // Un espacio crudo en un segmento de URI es inválido; escapado, es %20.
        Assert.DoesNotContain(" ", espia.UriCapturada!.AbsolutePath);
        Assert.Contains("reporte%20final.xlsx", espia.UriCapturada.PathAndQuery);
    }

    [Fact]
    public async Task IntentoDeTraversal_QuedaReducidoAlNombreDelArchivo()
    {
        var (almacen, espia) = Crear();

        await almacen.SubirAsync(new MemoryStream([1, 2, 3]), "../../etc/passwd.xlsx", CancellationToken.None);

        // Path.GetFileName (semántica Linux) descarta todo antes del último "/".
        Assert.EndsWith("passwd.xlsx", espia.UriCapturada!.AbsolutePath);
        Assert.DoesNotContain("..", espia.UriCapturada.AbsolutePath);
    }
}

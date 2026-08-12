using System.Net;
using Control.Api;

namespace Reto.Tests;

/// <summary>
/// El nombre de archivo lo elige el usuario — frecuente en español con tildes y
/// espacios ("Catálogo Q1.xlsx"). Sin escapar, esos caracteres rompen la URI que
/// se arma para el filer de SeaweedFS o la truncan en un punto inesperado.
/// Unitario: intercepta el HttpClient, no necesita SeaweedFS real.
/// </summary>
public class AlmacenCargasSeaweedFsTests
{
    /// <summary>Envoltorio que lanza en Seek — reproduce HttpBaseStream, el stream real de red que expuso el bug.</summary>
    private sealed class StreamNoSeekable(Stream interno) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => interno.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
    }

    private sealed class HandlerEspia : HttpMessageHandler
    {
        public Uri? UriCapturada { get; private set; }
        public byte[]? ContenidoDescarga { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UriCapturada = request.RequestUri;

            if (ContenidoDescarga is null)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));

            var respuesta = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StreamNoSeekable(new MemoryStream(ContenidoDescarga)))
            };
            return Task.FromResult(respuesta);
        }
    }

    private static (AlmacenCargasSeaweedFs Almacen, HandlerEspia Espia) Crear()
    {
        var espia = new HandlerEspia();
        var http = new HttpClient(espia) { BaseAddress = new Uri("http://seaweedfs:8888/") };
        return (new AlmacenCargasSeaweedFs(http), espia);
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

    /// <summary>
    /// Bug real encontrado en vivo (no en test): ExcelReaderFactory.CreateReader
    /// exige Seek — un .xlsx es un zip, hace falta leer el directorio central al
    /// final antes de leer las entradas. El stream de red (HttpBaseStream) no
    /// soporta Seek y tira NotSupportedException recién al intentar parsear el
    /// Excel, no al descargar. DescargarAsync debe devolver un stream seekable
    /// sin importar si el de la red lo era.
    /// </summary>
    [Fact]
    public async Task Descargar_ContraStreamDeRedNoSeekable_DevuelveStreamSeekable()
    {
        var (almacen, espia) = Crear();
        espia.ContenidoDescarga = [1, 2, 3, 4, 5];

        await using var contenido = await almacen.DescargarAsync("seaweed://cargas/x/archivo.xlsx");

        Assert.True(contenido.CanSeek);
    }

    [Fact]
    public async Task Descargar_DevuelveElContenidoCompleto()
    {
        var (almacen, espia) = Crear();
        byte[] esperado = [10, 20, 30, 40];
        espia.ContenidoDescarga = esperado;

        await using var contenido = await almacen.DescargarAsync("seaweed://cargas/x/archivo.xlsx");
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria);

        Assert.Equal(esperado, memoria.ToArray());
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Control.Api;

namespace CargaMasiva.Tests;

/// <summary>§2.4b — extensión y tamaño. Lógica pura: no necesita base ni contenedores.</summary>
public class ValidacionArchivoTests
{
    [Theory]
    [InlineData("catalogo.xlsx", 1024, null)]
    [InlineData("catalogo.XLSX", 1024, "mayúsculas también valen")]
    public void ArchivoValido_NoDevuelveError(string nombre, long bytes, string? _)
    {
        Assert.Null(ServicioCargas.ValidarArchivo(nombre, bytes, tamanoMaximoMb: 25));
    }

    [Theory]
    [InlineData("catalogo.csv")]
    [InlineData("catalogo.xls")]
    [InlineData("catalogo.xlsx.exe")]
    [InlineData("catalogo")]
    public void ExtensionDistintaDeXlsx_SeRechaza(string nombre)
    {
        Assert.Contains(".xlsx", ServicioCargas.ValidarArchivo(nombre, 1024, 25));
    }

    [Fact]
    public void ArchivoVacio_SeRechaza()
    {
        Assert.Contains("vacío", ServicioCargas.ValidarArchivo("catalogo.xlsx", 0, 25));
    }

    [Fact]
    public void ArchivoQueSuperaElMaximo_SeRechazaConElNumeroConfigurado()
    {
        var error = ServicioCargas.ValidarArchivo("catalogo.xlsx", 26L * 1024 * 1024, tamanoMaximoMb: 25);

        Assert.Contains("25 MB", error);
    }

    [Fact]
    public void ElMaximoEsConfigurable()
    {
        var bytes = 26L * 1024 * 1024;

        Assert.NotNull(ServicioCargas.ValidarArchivo("catalogo.xlsx", bytes, tamanoMaximoMb: 25));
        Assert.Null(ServicioCargas.ValidarArchivo("catalogo.xlsx", bytes, tamanoMaximoMb: 50));
    }
}

/// <summary>
/// La extensión la controla el cliente; esto prueba el contenido real. Un
/// archivo renombrado a .xlsx pasa ValidarArchivo pero no tiene firma ZIP.
/// </summary>
public class ValidacionFirmaTests
{
    [Fact]
    public async Task ContenidoZip_PasaLaValidacion()
    {
        Stream contenido = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]);

        Assert.Null(await ServicioCargas.ValidarFirmaAsync(contenido));
    }

    [Fact]
    public async Task TextoRenombradoAXlsx_SeRechazaPorFirma()
    {
        Stream contenido = new MemoryStream("esto no es un excel, es texto plano"u8.ToArray());

        Assert.Contains("firma binaria", await ServicioCargas.ValidarFirmaAsync(contenido));
    }

    [Fact]
    public async Task ArchivoDeMenosDe4Bytes_SeRechaza()
    {
        Stream contenido = new MemoryStream([0x50, 0x4B]);

        Assert.NotNull(await ServicioCargas.ValidarFirmaAsync(contenido));
    }

    [Fact]
    public async Task DespuesDeValidar_ElStreamQuedaEnPosicionCero()
    {
        Stream contenido = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x01, 0x02]);

        await ServicioCargas.ValidarFirmaAsync(contenido);

        Assert.Equal(0, contenido.Position);
    }
}

/// <summary>
/// §2️⃣ de punta a punta por el gateway: sube, registra en Pendiente y encola.
/// Requiere el stack levantado — <c>docker compose up -d</c>.
/// </summary>
[Collection("Gateway")]
public sealed class CargasTests(GatewayFixture fixture)
{
    private static readonly string RutaMuestra =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "carga_masiva_productos.xlsx"));

    private HttpRequestMessage Subida(string rutaArchivo, string nombreEnvio) =>
        SubidaBytes(File.ReadAllBytes(rutaArchivo), nombreEnvio);

    private HttpRequestMessage SubidaBytes(byte[] bytes, string nombreEnvio)
    {
        var contenido = new MultipartFormDataContent
        {
            { new ByteArrayContent(bytes), "archivo", nombreEnvio }
        };

        var peticion = new HttpRequestMessage(HttpMethod.Post, "/cargas") { Content = contenido };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.AccessToken);
        return peticion;
    }

    private HttpRequestMessage Get(string ruta)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.AccessToken);
        return peticion;
    }

    [Fact]
    public async Task Subir_XlsxValido_QuedaRegistradaEnPendienteYConRutaDeSeaweed()
    {
        var respuesta = await fixture.Cliente.SendAsync(Subida(RutaMuestra, "carga_masiva_productos.xlsx"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var creada = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var idCarga = creada.RootElement.GetProperty("idCarga").GetInt32();
        Assert.Equal("Pendiente", creada.RootElement.GetProperty("estado").GetString());

        var detalle = await fixture.Cliente.SendAsync(Get($"/cargas/{idCarga}"));
        detalle.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await detalle.Content.ReadAsStringAsync());
        Assert.StartsWith("seaweed://", json.RootElement.GetProperty("rutaArchivo").GetString());
        // Auditoría de quién y cuándo (§2️⃣).
        Assert.Equal("admin@reto.local", json.RootElement.GetProperty("carga").GetProperty("usuario").GetString());
    }

    [Fact]
    public async Task Subir_ConExtensionInvalida_Da400()
    {
        var respuesta = await fixture.Cliente.SendAsync(Subida(RutaMuestra, "catalogo.csv"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// La extensión sola no protege nada — el cliente la elige. Un .txt renombrado
    /// a .xlsx pasa el primer filtro y debe caer en la verificación de firma ZIP.
    /// </summary>
    [Fact]
    public async Task Subir_TextoPlanoRenombradoAXlsx_Da400PorFirma()
    {
        var bytes = "esto no es un excel, es texto plano disfrazado"u8.ToArray();

        var respuesta = await fixture.Cliente.SendAsync(SubidaBytes(bytes, "catalogo.xlsx"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("firma binaria", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Historial_DevuelveLasCargasDelMasRecienteAlMasViejo()
    {
        var respuesta = await fixture.Cliente.SendAsync(Get("/cargas?limite=5"));
        respuesta.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray().Select(c => c.GetProperty("idCarga").GetInt32()).ToArray();

        Assert.Equal(ids.OrderByDescending(i => i), ids);
    }

    [Fact]
    public async Task Detalle_DeUnaCargaInexistente_Da404()
    {
        var respuesta = await fixture.Cliente.SendAsync(Get("/cargas/999999"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// Bloque 6 de punta a punta: sube el archivo real y deja correr el
    /// consumidor real — descarga de SeaweedFS, sp_resolver_periodo, dedup,
    /// sp_insertar_data_procesada, transición de estado, publicación a
    /// notificaciones. Nada de esto se mockea.
    ///
    /// La primera vez que corre contra una base recién levantada (el escenario
    /// real de un evaluador con "docker compose up"), los tres periodos del
    /// archivo están libres y el resultado es el determinista de design.md §C5:
    /// 154 insertados / 46 Existente, Finalizado. Si se repite sin resetear la
    /// base, esos periodos ya quedaron tomados por la corrida anterior y el
    /// resultado correcto es Rechazada con las 200 filas auditadas — es la
    /// MISMA regla de negocio (§3.3) actuando, no un fallo del test. Por eso se
    /// afirma primero el invariante que se sostiene siempre, y después el
    /// resultado exacto de cada una de las dos ramas legítimas.
    /// </summary>
    [Fact]
    public async Task ArchivoDeMuestra_ProcesadoPorElConsumidorReal_TerminaEnFinalizadoORechazada()
    {
        var subida = await fixture.Cliente.SendAsync(Subida(RutaMuestra, "carga_masiva_productos.xlsx"));
        subida.EnsureSuccessStatusCode();
        using var creada = JsonDocument.Parse(await subida.Content.ReadAsStringAsync());
        var idCarga = creada.RootElement.GetProperty("idCarga").GetInt32();

        JsonElement detalle = default;
        string estado;
        var limite = DateTime.UtcNow.AddSeconds(20);
        do
        {
            // 1.5s y no algo más agresivo: LimitePorUsuario es 60/min compartido
            // por TODO lo que esta clase hace con el mismo token — un sondeo cada
            // 500ms (hasta 60 requests solo) lo agotaba junto con el resto de la
            // suite. Es además el comportamiento correcto de un cliente real: nadie
            // debería sondear un estado cada 500ms.
            await Task.Delay(1500);
            var respuesta = await fixture.Cliente.SendAsync(Get($"/cargas/{idCarga}"));
            respuesta.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            detalle = json.RootElement.Clone();
            estado = detalle.GetProperty("carga").GetProperty("estado").GetString()!;
        }
        while (estado is "Pendiente" or "EnProceso" && DateTime.UtcNow < limite);

        var carga = detalle.GetProperty("carga");
        var insertadas = carga.GetProperty("filasInsertadas").GetInt32();
        var rechazadas = carga.GetProperty("filasRechazadas").GetInt32();

        Assert.Contains(estado, (string[])["Finalizado", "Rechazada"]);
        Assert.Equal(200, insertadas + rechazadas);

        if (estado == "Finalizado")
        {
            Assert.Equal(154, insertadas);
            Assert.Equal(46, rechazadas);

            // Cada carga_periodo queda con su propio conteo, no en 0 (ManejadorCarga
            // lo completa después del INSERT masivo: el SP no lo sabe todavía).
            var periodos = detalle.GetProperty("periodos").EnumerateArray().ToList();
            Assert.Equal(3, periodos.Count);
            Assert.Equal(154, periodos.Sum(p => p.GetProperty("filasInsertadas").GetInt32()));
            Assert.All(periodos, p => Assert.Equal("Aceptado", p.GetProperty("estado").GetString()));
        }
        else
        {
            Assert.Equal(0, insertadas);
            Assert.Equal(200, rechazadas);
        }
    }
}

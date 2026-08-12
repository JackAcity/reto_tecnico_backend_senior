using System.Text;
using BuildingBlocks;
using Control.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Reto.Tests;

file sealed class AlmacenFalso : IAlmacenCargas
{
    public Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct) =>
        Task.FromResult($"seaweed://cargas/prueba/{nombreArchivo}");

    public Task<Stream> DescargarAsync(string ruta, CancellationToken ct) =>
        Task.FromResult<Stream>(new MemoryStream());
}

/// <summary>
/// <paramref name="falla"/> simula un fallo de infraestructura esperado (Resultado.Fallo,
/// design.md §D4). <paramref name="bugInesperado"/> simula un bug real ajeno a la
/// publicación — debe propagarse como excepción, no confundirse con lo anterior.
/// </summary>
file sealed class PublicadorFalso(bool falla = false, bool bugInesperado = false) : IPublicadorCargas
{
    public List<(MensajeCarga Mensaje, string CorrelationId)> Publicados { get; } = [];

    public Task<Resultado> PublicarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct)
    {
        if (bugInesperado)
            throw new NullReferenceException("bug simulado, no relacionado con el fallo de publicación");

        if (falla)
            return Task.FromResult(Resultado.Fallo("broker caído"));

        Publicados.Add((mensaje, correlationId));
        return Task.FromResult(Resultado.Exito());
    }
}

/// <summary>
/// §C7 — el dual write. Publicar en el broker y escribir en la base no comparten
/// transacción: lo que se prueba acá es que una publicación fallida no deja la
/// carga colgada en Pendiente para siempre.
/// Requiere la base levantada; se revierte al terminar.
/// </summary>
public sealed class RegistroDeCargaTests : IAsyncLifetime
{
    private static string Cadena =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Database=reto;Username=reto;Password=cambiar_en_local";

    private NpgsqlConnection _cn = null!;
    private ControlDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _cn = new NpgsqlConnection(Cadena);
        await _cn.OpenAsync();
        _db = new ControlDbContext(new DbContextOptionsBuilder<ControlDbContext>()
            .UseNpgsql(_cn)
            .UseSnakeCaseNamingConvention()
            .Options);
        await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.RollbackTransactionAsync();
        await _db.DisposeAsync();
        await _cn.DisposeAsync();
    }

    private ServicioCargas Servicio(IPublicadorCargas publicador) =>
        new(new RepositorioCargasEf(_db), new AlmacenFalso(), publicador, NullLogger<ServicioCargas>.Instance);

    private static Stream Contenido() => new MemoryStream(Encoding.UTF8.GetBytes("contenido de prueba"));

    [Fact]
    public async Task PublicacionCorrecta_DejaLaCargaEnPendienteYEncolaElContratoDelEnunciado()
    {
        var publicador = new PublicadorFalso();

        var resultado = await Servicio(publicador).RegistrarAsync(
            Contenido(), "catalogo.xlsx", 1024, "admin@reto.local", "corr-1");

        Assert.Null(resultado.Error);
        Assert.Equal(nameof(EstadoRegistroCarga.Pendiente), resultado.Estado);

        var (mensaje, correlationId) = Assert.Single(publicador.Publicados);
        Assert.Equal("corr-1", correlationId);

        // El contrato es literal (§2️⃣): idCarga, rutaArchivo, usuario. El
        // correlationId viaja aparte, como cabecera AMQP.
        Assert.Equal(resultado.IdCarga, mensaje.IdCarga);
        Assert.Equal("admin@reto.local", mensaje.Usuario);
        Assert.StartsWith("seaweed://", mensaje.RutaArchivo);
    }

    [Fact]
    public async Task PublicacionFallida_DejaLaCargaEnFallidaConElError()
    {
        var resultado = await Servicio(new PublicadorFalso(falla: true)).RegistrarAsync(
            Contenido(), "catalogo.xlsx", 1024, "admin@reto.local", "corr-2");

        Assert.Equal(nameof(EstadoRegistroCarga.Fallida), resultado.Estado);
        Assert.Contains("broker caído", resultado.Error);

        // Y queda auditada: el usuario la ve en el historial en vez de esperar
        // indefinidamente por una carga que nunca se va a procesar.
        var enBase = await _db.CargaArchivos.SingleAsync(c => c.Id == resultado.IdCarga);
        Assert.Equal(EstadoRegistroCarga.Fallida, enBase.Estado);
        Assert.NotNull(enBase.FechaFin);
    }

    [Fact]
    public async Task LaCargaGuardaLaAuditoriaDeQuienYCuando()
    {
        var antes = DateTimeOffset.UtcNow.AddSeconds(-1);

        var resultado = await Servicio(new PublicadorFalso()).RegistrarAsync(
            Contenido(), "catalogo.xlsx", 4096, "operador@reto.local", "corr-3");

        var enBase = await _db.CargaArchivos.SingleAsync(c => c.Id == resultado.IdCarga);
        Assert.Equal("operador@reto.local", enBase.Usuario);
        Assert.Equal(4096, enBase.TamanoBytes);
        Assert.Equal("catalogo.xlsx", enBase.NombreArchivo);
        Assert.True(enBase.FechaRegistro >= antes);
        Assert.Equal("corr-3", enBase.CorrelationId);
    }

    /// <summary>
    /// Cierra la Requirement "Un bug real en el publicador ya no se confunde con
    /// un fallo esperado" (resultado-sin-excepciones): un bug ajeno al fallo de
    /// publicación esperado se propaga sin pasar por ningún catch de RegistrarAsync.
    /// </summary>
    [Fact]
    public async Task PublicacionConBugInesperado_SePropagaComoExcepcion()
    {
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            Servicio(new PublicadorFalso(bugInesperado: true)).RegistrarAsync(
                Contenido(), "catalogo.xlsx", 1024, "admin@reto.local", "corr-4"));
    }
}

using System.Text;
using Almacenamiento;
using CargaMasiva.Domain;
using Control.Api;
using Mensajeria;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Persistencia;

namespace CargaMasiva.Tests;

file sealed class AlmacenFalso : IAlmacenArchivos
{
    public Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default) =>
        Task.FromResult($"seaweed://cargas/prueba/{nombreArchivo}");

    public Task<Stream> DescargarAsync(string ruta, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream());
}

file sealed class PublicadorFalso(bool falla = false) : IPublicador
{
    public List<(string RoutingKey, object Mensaje, string CorrelationId)> Publicados { get; } = [];

    public Task PublicarAsync<T>(string routingKey, T mensaje, string correlationId, CancellationToken ct = default)
    {
        if (falla)
            throw new InvalidOperationException("broker caído");

        Publicados.Add((routingKey, mensaje!, correlationId));
        return Task.CompletedTask;
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
    private RetoDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _cn = new NpgsqlConnection(Cadena);
        await _cn.OpenAsync();
        _db = new RetoDbContext(new DbContextOptionsBuilder<RetoDbContext>()
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

    private ServicioCargas Servicio(IPublicador publicador) =>
        new(_db, new AlmacenFalso(), publicador, NullLogger<ServicioCargas>.Instance);

    private static Stream Contenido() => new MemoryStream(Encoding.UTF8.GetBytes("contenido de prueba"));

    [Fact]
    public async Task PublicacionCorrecta_DejaLaCargaEnPendienteYEncolaElContratoDelEnunciado()
    {
        var publicador = new PublicadorFalso();

        var resultado = await Servicio(publicador).RegistrarAsync(
            Contenido(), "catalogo.xlsx", 1024, "admin@reto.local", "corr-1");

        Assert.Null(resultado.Error);
        Assert.Equal(nameof(EstadoCarga.Pendiente), resultado.Estado);

        var (routingKey, mensaje, correlationId) = Assert.Single(publicador.Publicados);
        Assert.Equal(Topologia.RkCarga, routingKey);
        Assert.Equal("corr-1", correlationId);

        // El contrato es literal (§2️⃣): idCarga, rutaArchivo, usuario. El
        // correlationId viaja aparte, como cabecera AMQP.
        var carga = Assert.IsType<MensajeCarga>(mensaje);
        Assert.Equal(resultado.IdCarga, carga.IdCarga);
        Assert.Equal("admin@reto.local", carga.Usuario);
        Assert.StartsWith("seaweed://", carga.RutaArchivo);
    }

    [Fact]
    public async Task PublicacionFallida_DejaLaCargaEnFallidaConElError()
    {
        var resultado = await Servicio(new PublicadorFalso(falla: true)).RegistrarAsync(
            Contenido(), "catalogo.xlsx", 1024, "admin@reto.local", "corr-2");

        Assert.Equal(nameof(EstadoCarga.Fallida), resultado.Estado);
        Assert.Contains("broker caído", resultado.Error);

        // Y queda auditada: el usuario la ve en el historial en vez de esperar
        // indefinidamente por una carga que nunca se va a procesar.
        var enBase = await _db.CargaArchivos.SingleAsync(c => c.Id == resultado.IdCarga);
        Assert.Equal(EstadoCarga.Fallida, enBase.Estado);
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
}

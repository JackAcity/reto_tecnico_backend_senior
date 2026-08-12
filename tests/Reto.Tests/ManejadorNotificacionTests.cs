using BuildingBlocks;
using Microsoft.Extensions.Logging.Abstractions;
using Notificaciones.Api;

namespace Reto.Tests;

file sealed class RepositorioNotificacionesFalso(CargaPorNotificar carga) : IRepositorioNotificaciones
{
    public int Guardados { get; private set; }

    public Task<CargaPorNotificar> ObtenerAsync(int idCarga, CancellationToken ct) => Task.FromResult(carga);

    public Task GuardarCambiosAsync(CancellationToken ct)
    {
        Guardados++;
        return Task.CompletedTask;
    }
}

file sealed class EnviadorCorreoFalso : IEnviadorCorreo
{
    public List<(string Destinatario, int IdCarga)> Envios { get; } = [];

    public Task EnviarResumenCargaAsync(string destinatario, int idCarga, int filasInsertadas, int filasRechazadas, DateTimeOffset fechaFin, CancellationToken ct = default)
    {
        Envios.Add((destinatario, idCarga));
        return Task.CompletedTask;
    }
}

public sealed class ManejadorNotificacionTests
{
    private static MensajeNotificacion Mensaje() => new(23, "operador@reto.local", DateTimeOffset.UtcNow);

    [Fact]
    public async Task CargaFinalizada_EnviaResumenYLaMarcaNotificada()
    {
        var carga = new CargaPorNotificar
        {
            Id = 23,
            Usuario = "operador@reto.local",
            FilasInsertadas = 9,
            FilasRechazadas = 2,
            Estado = EstadoNotificacionCarga.Finalizado
        };
        var repositorio = new RepositorioNotificacionesFalso(carga);
        var correo = new EnviadorCorreoFalso();
        var manejador = new ManejadorNotificacion(repositorio, correo, NullLogger<ManejadorNotificacion>.Instance);

        await manejador.ProcesarAsync(Mensaje(), default);

        Assert.Single(correo.Envios);
        Assert.Equal(EstadoNotificacionCarga.Notificado, carga.Estado);
        Assert.Equal(1, repositorio.Guardados);
    }

    [Fact]
    public async Task CargaNoFinalizada_NoEnviaNiModificaElEstado()
    {
        var carga = new CargaPorNotificar { Id = 23, Estado = EstadoNotificacionCarga.EnProceso };
        var repositorio = new RepositorioNotificacionesFalso(carga);
        var correo = new EnviadorCorreoFalso();
        var manejador = new ManejadorNotificacion(repositorio, correo, NullLogger<ManejadorNotificacion>.Instance);

        await manejador.ProcesarAsync(Mensaje(), default);

        Assert.Empty(correo.Envios);
        Assert.Equal(EstadoNotificacionCarga.EnProceso, carga.Estado);
        Assert.Equal(0, repositorio.Guardados);
    }
}
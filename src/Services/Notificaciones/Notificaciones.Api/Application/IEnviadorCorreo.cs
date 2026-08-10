namespace Notificaciones.Api;

/// <summary>Puerto de salida para notificar el resultado de una carga.</summary>
public interface IEnviadorCorreo
{
    Task EnviarResumenCargaAsync(
        string destinatario, int idCarga, int filasInsertadas, int filasRechazadas, DateTimeOffset fechaFin, CancellationToken ct = default);
}

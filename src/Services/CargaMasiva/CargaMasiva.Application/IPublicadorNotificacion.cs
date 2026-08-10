using BuildingBlocks;

namespace CargaMasiva.Application;

/// <summary>Publica la notificación final del procesamiento de una carga.</summary>
public interface IPublicadorNotificacion
{
    Task<Resultado> PublicarAsync(MensajeNotificacion mensaje, string correlationId, CancellationToken ct);
}

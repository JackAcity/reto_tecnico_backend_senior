using BuildingBlocks;
using CargaMasiva.Application;
using Mensajeria;

namespace CargaMasiva.Infrastructure;

/// <summary>Traduce el fallo esperado de RabbitMQ al resultado del caso de uso.</summary>
public sealed class PublicadorNotificacionRabbit(PublicadorRabbit publicador) : IPublicadorNotificacion
{
    public async Task<Resultado> PublicarAsync(MensajeNotificacion mensaje, string correlationId, CancellationToken ct)
    {
        try
        {
            await publicador.PublicarAsync(Topologia.RkNotificacion, mensaje, correlationId, ct);
            return Resultado.Exito();
        }
        catch (FalloPublicacionRabbitException ex)
        {
            return Resultado.Fallo(ex.Message);
        }
    }
}

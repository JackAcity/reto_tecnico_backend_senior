using BuildingBlocks;

namespace Control.Api;

/// <summary>Traduce un fallo esperado de RabbitMQ para el caso de uso de registro.</summary>
public sealed class PublicadorCargasRabbit(PublicadorRabbit publicador) : IPublicadorCargas
{
    public async Task<Resultado> PublicarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct)
    {
        try
        {
            await publicador.PublicarAsync(TopologiaRabbit.RkCarga, mensaje, correlationId, ct);
            return Resultado.Exito();
        }
        catch (FalloPublicacionRabbitException ex)
        {
            return Resultado.Fallo(ex.Message);
        }
    }
}

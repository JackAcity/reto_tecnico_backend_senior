using Almacenamiento;
using BuildingBlocks;
using Mensajeria;

namespace Control.Api;

/// <summary>Adaptador SeaweedFS del puerto de carga de archivos.</summary>
public sealed class AlmacenCargasSeaweedFs(IAlmacenArchivos almacen) : IAlmacenCargas
{
    public Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct) =>
        almacen.SubirAsync(contenido, nombreArchivo, ct);
}

/// <summary>Traduce un fallo esperado de RabbitMQ para el caso de uso de registro.</summary>
public sealed class PublicadorCargasRabbit(PublicadorRabbit publicador) : IPublicadorCargas
{
    public async Task<Resultado> PublicarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct)
    {
        try
        {
            await publicador.PublicarAsync(Topologia.RkCarga, mensaje, correlationId, ct);
            return Resultado.Exito();
        }
        catch (FalloPublicacionRabbitException ex)
        {
            return Resultado.Fallo(ex.Message);
        }
    }
}

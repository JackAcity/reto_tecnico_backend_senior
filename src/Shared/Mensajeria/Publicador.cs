using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Mensajeria;

public interface IPublicador
{
    /// <summary>Publica y espera la confirmación del broker. Si vuelve sin excepción, el mensaje está en la cola.</summary>
    Task PublicarAsync<T>(string routingKey, T mensaje, string correlationId, CancellationToken ct = default);
}

/// <summary>
/// Publicador con <b>publisher confirms</b>. Sin confirmación no habría forma de
/// saber si el mensaje se perdió, y el §C7 depende justamente de eso: si la
/// publicación falla, la carga pasa a estado terminal Fallida.
/// </summary>
public sealed class PublicadorRabbit(IOptions<OpcionesRabbit> opciones) : IPublicador, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OpcionesRabbit _opciones = opciones.Value;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;
    private IChannel? _canal;

    public async Task PublicarAsync<T>(string routingKey, T mensaje, string correlationId, CancellationToken ct = default)
    {
        var canal = await CanalAsync(ct);
        var propiedades = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,   // sobrevive a un reinicio del broker
            CorrelationId = correlationId,
            MessageId = Guid.NewGuid().ToString("N")
        };

        await canal.BasicPublishAsync(
            Topologia.Exchange, routingKey, mandatory: true, propiedades,
            JsonSerializer.SerializeToUtf8Bytes(mensaje, Json), ct);
    }

    private async Task<IChannel> CanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true })
            return _canal;

        await _candado.WaitAsync(ct);
        try
        {
            if (_canal is { IsOpen: true })
                return _canal;

            _conexion ??= await new ConnectionFactory
            {
                HostName = _opciones.Host,
                Port = _opciones.Puerto,
                UserName = _opciones.Usuario,
                Password = _opciones.Password
            }.CreateConnectionAsync(ct);

            _canal = await _conexion.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                ct);

            await Topologia.DeclararAsync(_canal, _opciones.ReintentoSegundos, ct);
            return _canal;
        }
        finally
        {
            _candado.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.DisposeAsync();
        if (_conexion is not null) await _conexion.DisposeAsync();
        _candado.Dispose();
    }
}

public static class MensajeriaExtensiones
{
    public static IServiceCollection AddMensajeria(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.Configure<OpcionesRabbit>(config.GetSection("RabbitMq"));
        servicios.AddSingleton<IPublicador, PublicadorRabbit>();
        return servicios;
    }
}

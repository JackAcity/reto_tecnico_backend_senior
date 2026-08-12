using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Control.Api;

/// <summary>
/// Publica con confirms y <c>mandatory</c> para que una ruta sin cola falle en vez de perder el mensaje.
/// El handler de retorno sólo aporta diagnóstico estructurado.
/// </summary>
public sealed class PublicadorRabbit(IOptions<OpcionesRabbit> opciones, ILogger<PublicadorRabbit> log)
    : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OpcionesRabbit _opciones = opciones.Value;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;
    private IChannel? _canal;

    public async Task PublicarAsync<T>(string routingKey, T mensaje, string correlationId, CancellationToken ct = default)
    {
        try
        {
            var canal = await CanalAsync(ct);
            var propiedades = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString("N"),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await canal.BasicPublishAsync(
                TopologiaRabbit.Exchange, routingKey, mandatory: true, propiedades,
                JsonSerializer.SerializeToUtf8Bytes(mensaje, Json), ct);

        }
        catch (RabbitMQClientException ex)
        {
            throw new FalloPublicacionRabbitException(ex.Message, ex);
        }
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

            _opciones.Validar();
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
            _canal.BasicReturnAsync += AlRetornarMensajeAsync;

            await TopologiaRabbit.DeclararAsync(_canal, _opciones.ReintentoSegundos, ct);
            return _canal;
        }
        finally
        {
            _candado.Release();
        }
    }

    private Task AlRetornarMensajeAsync(object? sender, BasicReturnEventArgs e)
    {
        log.LogError(
            "Mensaje sin ruta: exchange={Exchange} routingKey={RoutingKey} replyCode={ReplyCode} replyText={ReplyText} messageId={MessageId}. " +
            "La topología se declara completa antes de publicar — esto indica un defecto de código, no una falla transitoria.",
            e.Exchange, e.RoutingKey, e.ReplyCode, e.ReplyText, e.BasicProperties.MessageId);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.DisposeAsync();
        if (_conexion is not null) await _conexion.DisposeAsync();
        _candado.Dispose();
    }
}

/// <summary>Fallo esperado de RabbitMQ traducido por el adaptador.</summary>
public sealed class FalloPublicacionRabbitException(string mensaje, Exception interna)
    : Exception(mensaje, interna);

public static class MensajeriaExtensiones
{
    public static IServiceCollection AddMensajeriaRabbit(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.Configure<OpcionesRabbit>(config.GetSection("RabbitMq"));
        servicios.AddSingleton<PublicadorRabbit>();
        return servicios;
    }
}

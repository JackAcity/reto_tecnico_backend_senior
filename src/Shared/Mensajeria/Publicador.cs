using System.Text.Json;
using BuildingBlocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Mensajeria;

public interface IPublicador
{
    /// <summary>
    /// Publica y espera la confirmación del broker. Un fallo de infraestructura
    /// esperado (broker caído, routing key sin cola vinculada — ver
    /// <see cref="PublicadorRabbit"/>) se comunica como <c>Resultado.Fallo(...)</c>,
    /// no como excepción (design.md §D4 de arquitectura-hexagonal-transversal) — el
    /// llamador ya debe tratar un <c>Resultado</c> fallido como "no se pudo encolar"
    /// (§C7). Una excepción que sí se propaga desde este método es un bug, no un
    /// fallo esperado.
    /// </summary>
    Task<Resultado> PublicarAsync<T>(string routingKey, T mensaje, string correlationId, CancellationToken ct = default);
}

/// <summary>
/// Publicador con <b>publisher confirms</b>. Con <c>publisherConfirmationTrackingEnabled</c>
/// (RabbitMQ.Client 7), el cliente correlaciona internamente el "basic.return" con
/// el confirm pendiente: un routing key sin cola vinculada (<c>mandatory: true</c>)
/// no se pierde en silencio — hace fallar <see cref="PublicarAsync{T}"/> con
/// <see cref="RabbitMQ.Client.Exceptions.PublishReturnException"/>, que
/// <c>ServicioCargas.RegistrarAsync</c> ya captura y trata como fallo de
/// publicación (§C7: la carga pasa a <c>Fallida</c>). Verificado con un routing
/// key inexistente contra un broker real — no es una suposición de la librería,
/// es el comportamiento observado en RabbitMQ.Client 7.2.2.
///
/// El log adicional de <see cref="AlRetornarMensajeAsync"/> no es la red de
/// seguridad (la excepción ya lo es): es una línea con campos estructurados
/// (exchange, routing key, reply code) para diagnosticar más rápido cuál mensaje
/// se devolvió, sin tener que parsear el texto de la excepción.
/// </summary>
public sealed class PublicadorRabbit(IOptions<OpcionesRabbit> opciones, ILogger<PublicadorRabbit> log)
    : IPublicador, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly OpcionesRabbit _opciones = opciones.Value;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;
    private IChannel? _canal;

    public async Task<Resultado> PublicarAsync<T>(string routingKey, T mensaje, string correlationId, CancellationToken ct = default)
    {
        try
        {
            var canal = await CanalAsync(ct);
            var propiedades = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,   // sobrevive a un reinicio del broker
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString("N"),
                // "Fecha de registro" del evento, en el campo AMQP estándar para eso —
                // no hace falta reinventarlo dentro del JSON, que además el enunciado
                // define palabra por palabra (§2️⃣/§3️⃣, design.md §M2).
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await canal.BasicPublishAsync(
                Topologia.Exchange, routingKey, mandatory: true, propiedades,
                JsonSerializer.SerializeToUtf8Bytes(mensaje, Json), ct);

            return Resultado.Exito();
        }
        catch (RabbitMQClientException ex)
        {
            // Fallo de infraestructura esperado (broker caído, routing key sin cola
            // vinculada — PublishReturnException). Cualquier otra excepción (un bug
            // real de serialización, por ejemplo) no es de este tipo y se propaga
            // sin capturarse (design.md §D4).
            return Resultado.Fallo(ex.Message);
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

            await Topologia.DeclararAsync(_canal, _opciones.ReintentoSegundos, ct);
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

public static class MensajeriaExtensiones
{
    public static IServiceCollection AddMensajeria(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.Configure<OpcionesRabbit>(config.GetSection("RabbitMq"));
        servicios.AddSingleton<IPublicador, PublicadorRabbit>();
        return servicios;
    }
}

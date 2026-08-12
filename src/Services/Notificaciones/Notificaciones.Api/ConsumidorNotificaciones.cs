using System.Text.Json;
using BuildingBlocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace Notificaciones.Api;

/// <summary>
/// §4️⃣. Mismo patrón que ConsumidorCargaMasiva (prefetch 1, ack manual, tope de
/// reintentos vía x-death), con una diferencia real: al agotar los intentos NO
/// hay transición de estado que marcar. MaquinaEstados solo permite
/// Finalizado → Notificado — no existe un Finalizado → Fallida, porque la carga
/// ya tuvo éxito en lo que importa (§3.3: los datos quedaron insertados). Un
/// correo que nunca sale es un problema operativo, no de negocio: se audita
/// fuerte en el log y el mensaje queda en notificaciones.muertos para revisión
/// manual, sin tocar carga_archivo.
/// </summary>
public sealed class ConsumidorNotificaciones(
    IServiceScopeFactory scopeFactory,
    IOptions<OpcionesRabbit> opciones,
    ILogger<ConsumidorNotificaciones> log) : BackgroundService
{
    public const int MaxIntentos = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IConnection? _conexion;
    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = opciones.Value;
        opts.Validar();
        _conexion = await new ConnectionFactory
        {
            HostName = opts.Host,
            Port = opts.Puerto,
            UserName = opts.Usuario,
            Password = opts.Password
        }.CreateConnectionAsync(ct);

        _canal = await _conexion.CreateChannelAsync(cancellationToken: ct);
        await TopologiaRabbit.DeclararAsync(_canal, opts.ReintentoSegundos, ct);
        await _canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, ct);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += async (_, ea) => await ProcesarEntregaAsync(ea, ct);

        await _canal.BasicConsumeAsync(TopologiaRabbit.ColaNotificaciones, autoAck: false, consumidor, ct);

        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private async Task ProcesarEntregaAsync(BasicDeliverEventArgs ea, CancellationToken ctHost)
    {
        var canal = _canal!;
        var correlationId = ea.BasicProperties.CorrelationId ?? "";
        using var _ = LogContext.PushProperty("CorrelationId", correlationId);

        MensajeNotificacion? mensaje = null;
        try
        {
            mensaje = JsonSerializer.Deserialize<MensajeNotificacion>(ea.Body.Span, Json);
            if (mensaje is null)
                throw new InvalidOperationException("Mensaje de notificación vacío o mal formado.");

            await using var alcance = scopeFactory.CreateAsyncScope();
            var manejador = alcance.ServiceProvider.GetRequiredService<ManejadorNotificacion>();
            await manejador.ProcesarAsync(mensaje, ctHost);

            await canal.BasicAckAsync(ea.DeliveryTag, multiple: false, ctHost);
        }
        catch (Exception ex)
        {
            var intentos = TopologiaRabbit.ContarIntentosPrevios(ea.BasicProperties.Headers, TopologiaRabbit.ColaNotificaciones);
            log.LogError(ex, "Fallo enviando notificación de carga {IdCarga}, intento {Intento}/{Max}",
                mensaje?.IdCarga, intentos + 1, MaxIntentos);

            if (intentos + 1 >= MaxIntentos)
            {
                log.LogCritical(
                    "Carga {IdCarga}: se agotaron los {Max} intentos de notificación. " +
                    "El usuario no recibirá el correo; el mensaje queda en {ColaMuertos} para revisión manual.",
                    mensaje?.IdCarga, MaxIntentos, TopologiaRabbit.ColaNotificacionesMuertos);

                var propiedades = new BasicProperties
                {
                    ContentType = ea.BasicProperties.ContentType,
                    CorrelationId = ea.BasicProperties.CorrelationId,
                    MessageId = ea.BasicProperties.MessageId,
                    DeliveryMode = ea.BasicProperties.DeliveryMode,
                    Headers = ea.BasicProperties.Headers
                };
                await canal.BasicPublishAsync(
                    TopologiaRabbit.Exchange, TopologiaRabbit.RkNotificacionMuerto, mandatory: false,
                    propiedades, ea.Body, ctHost);

                await canal.BasicAckAsync(ea.DeliveryTag, multiple: false, ctHost);
            }
            else
            {
                await canal.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ctHost);
            }
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_canal is not null) await _canal.CloseAsync(ct);
        if (_conexion is not null) await _conexion.CloseAsync(ct);
        await base.StopAsync(ct);
    }

    public override void Dispose()
    {
        _canal?.Dispose();
        _conexion?.Dispose();
        base.Dispose();
    }
}

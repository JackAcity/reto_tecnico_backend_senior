using System.Text.Json;
using BuildingBlocks;
using CargaMasiva.Application;
using CargaMasiva.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace CargaMasiva.Api;

/// <summary>
/// Consume una carga a la vez y confirma sólo al terminar, para permitir reentregas idempotentes.
/// </summary>
public sealed class ConsumidorCargaMasiva(
    IServiceScopeFactory scopeFactory,
    IOptions<OpcionesRabbit> opciones,
    ILogger<ConsumidorCargaMasiva> log) : BackgroundService
{
    /// <summary>Reintentos antes de enviar el mensaje a la cola de muertos.</summary>
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

        await _canal.BasicConsumeAsync(TopologiaRabbit.ColaCarga, autoAck: false, consumidor, ct);

        // El consumidor entrega por evento; el servicio debe permanecer activo.
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private async Task ProcesarEntregaAsync(BasicDeliverEventArgs ea, CancellationToken ctHost)
    {
        var canal = _canal!;
        var correlationId = ea.BasicProperties.CorrelationId ?? "";
        using var _ = LogContext.PushProperty("CorrelationId", correlationId);

        MensajeCarga? mensaje = null;
        try
        {
            mensaje = JsonSerializer.Deserialize<MensajeCarga>(ea.Body.Span, Json);
            if (mensaje is null)
                throw new InvalidOperationException("Mensaje de carga vacío o mal formado.");

            await using var alcance = scopeFactory.CreateAsyncScope();
            var manejador = alcance.ServiceProvider.GetRequiredService<ManejadorCarga>();
            await manejador.ProcesarAsync(mensaje, correlationId, ctHost);

            await canal.BasicAckAsync(ea.DeliveryTag, multiple: false, ctHost);
        }
        catch (Exception ex)
        {
            var intentos = TopologiaRabbit.ContarIntentosPrevios(ea.BasicProperties.Headers, TopologiaRabbit.ColaCarga);
            log.LogError(ex, "Fallo procesando carga {IdCarga}, intento {Intento}/{Max}",
                mensaje?.IdCarga, intentos + 1, MaxIntentos);

            if (intentos + 1 >= MaxIntentos)
            {
                // Publicar en la cola de muertos detiene el ciclo de TTL y reintento.
                // RabbitMQ exige propiedades mutables al republicar la entrega.
                var propiedades = new BasicProperties
                {
                    ContentType = ea.BasicProperties.ContentType,
                    CorrelationId = ea.BasicProperties.CorrelationId,
                    MessageId = ea.BasicProperties.MessageId,
                    DeliveryMode = ea.BasicProperties.DeliveryMode,
                    Headers = ea.BasicProperties.Headers
                };
                await canal.BasicPublishAsync(
                    TopologiaRabbit.Exchange, TopologiaRabbit.RkCargaMuerto, mandatory: false,
                    propiedades, ea.Body, ctHost);

                if (mensaje is not null)
                    await MarcarFallidaAsync(mensaje.IdCarga, ex.Message, ctHost);

                await canal.BasicAckAsync(ea.DeliveryTag, multiple: false, ctHost);
            }
            else
            {
                await canal.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ctHost);
            }
        }
    }

    private async Task MarcarFallidaAsync(int idCarga, string error, CancellationToken ct)
    {
        await using var alcance = scopeFactory.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<CargaMasivaDbContext>();
        var carga = await db.CargaArchivos.FindAsync([idCarga], ct);

        // Una reentrega puede llegar después de que otro intento alcance un estado terminal.
        if (carga is { Estado: CargaMasiva.Domain.EstadoCarga.Pendiente or CargaMasiva.Domain.EstadoCarga.EnProceso })
        {
            carga.Transicionar(CargaMasiva.Domain.EstadoCarga.Fallida);
            carga.MensajeError = $"Agotados los {MaxIntentos} intentos: {error}";
            await db.SaveChangesAsync(ct);
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

using System.Text.Json;
using BuildingBlocks;
using CargaMasiva.Application;
using Mensajeria;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace CargaMasiva.Api;

/// <summary>
/// El núcleo del reto (§3️⃣). Prefetch 1: cada carga puede ser un archivo grande,
/// no tiene sentido bajar N a la vez. Ack manual: solo se confirma cuando
/// <see cref="ManejadorCarga"/> terminó sin excepción — si el proceso muere a
/// mitad de camino, el mensaje vuelve a la cola (§C8, el consumidor es idempotente
/// vía la clave de negocio y el chequeo de estado).
/// </summary>
public sealed class ConsumidorCargaMasiva(
    IServiceScopeFactory scopeFactory,
    IOptions<OpcionesRabbit> opciones,
    ILogger<ConsumidorCargaMasiva> log) : BackgroundService
{
    /// <summary>Intentos sobre <c>carga_masiva</c> antes de darse por vencido y mandar a la cola de muertos.</summary>
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
        await Topologia.DeclararAsync(_canal, opts.ReintentoSegundos, ct);
        await _canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, ct);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += async (_, ea) => await ProcesarEntregaAsync(ea, ct);

        await _canal.BasicConsumeAsync(Topologia.ColaCarga, autoAck: false, consumidor, ct);

        // BasicConsumeAsync no bloquea: el propio BackgroundService debe quedar
        // vivo mientras el host corra, entregando el control al consumidor por evento.
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
            var intentos = Topologia.ContarIntentosPrevios(ea.BasicProperties.Headers, Topologia.ColaCarga);
            log.LogError(ex, "Fallo procesando carga {IdCarga}, intento {Intento}/{Max}",
                mensaje?.IdCarga, intentos + 1, MaxIntentos);

            if (intentos + 1 >= MaxIntentos)
            {
                // Un nack normal volvería a mandarlo al ciclo de reintento (DLX ->
                // TTL -> de vuelta a esta misma cola). Se publica a mano en la cola
                // de muertos para cortar el ciclo, y se audita en carga_archivo en
                // vez de dejar la carga colgada sin explicación.
                // BasicPublishAsync exige BasicProperties (mutable); la entrega solo
                // trae IReadOnlyBasicProperties, así que se copian los campos que
                // importan para diagnosticar el mensaje en la cola de muertos.
                var propiedades = new BasicProperties
                {
                    ContentType = ea.BasicProperties.ContentType,
                    CorrelationId = ea.BasicProperties.CorrelationId,
                    MessageId = ea.BasicProperties.MessageId,
                    DeliveryMode = ea.BasicProperties.DeliveryMode,
                    Headers = ea.BasicProperties.Headers
                };
                await canal.BasicPublishAsync(
                    Topologia.Exchange, Topologia.RkCargaMuerto, mandatory: false,
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
        var db = alcance.ServiceProvider.GetRequiredService<Persistencia.RetoDbContext>();
        var carga = await db.CargaArchivos.FindAsync([idCarga], ct);

        // Puede que ya haya transicionado a un estado terminal en un intento previo
        // (poco probable, pero MaquinaEstados.Validar lanzaría si se fuerza igual).
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

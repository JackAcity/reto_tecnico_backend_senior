using RabbitMQ.Client;

namespace Mensajeria;

public sealed class OpcionesRabbit
{
    public string Host { get; set; } = "";
    public int Puerto { get; set; } = 5672;
    public string Usuario { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Espera antes de reintentar un lote que falló, en segundos.</summary>
    public int ReintentoSegundos { get; set; } = 30;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new ConfiguracionMensajeriaException("Falta RabbitMq:Host.");
        if (string.IsNullOrWhiteSpace(Usuario)) throw new ConfiguracionMensajeriaException("Falta RabbitMq:Usuario.");
        if (string.IsNullOrWhiteSpace(Password)) throw new ConfiguracionMensajeriaException("Falta RabbitMq:Password.");
    }
}

/// <summary>Configuración inválida del adaptador RabbitMQ; el borde HTTP la trata como 500.</summary>
public sealed class ConfiguracionMensajeriaException(string mensaje) : Exception(mensaje);

/// <summary>
/// Un exchange topic y dos colas — el mínimo que el enunciado evalúa — más el
/// circuito de reintento, aplicado a LAS DOS colas por igual: un correo que
/// falla (Mailpit caído, por ejemplo) merece el mismo tope de reintentos que
/// un archivo que falla, no debe girar para siempre ni perderse en silencio.
/// Se declara desde los tres servicios: es idempotente, y así ninguno depende
/// de que otro haya arrancado antes.
/// </summary>
public static class Topologia
{
    public const string Exchange = "cargas";
    public const string ExchangeReintento = "cargas.reintento";

    public const string RkCarga = "carga.masiva";
    public const string RkNotificacion = "carga.notificacion";
    public const string RkCargaMuerto = "carga.masiva.muerto";
    public const string RkNotificacionMuerto = "carga.notificacion.muerto";

    public const string ColaCarga = "carga_masiva";
    public const string ColaNotificaciones = "notificaciones";
    public const string ColaCargaReintento = "carga_masiva.reintento";
    public const string ColaCargaMuertos = "carga_masiva.muertos";
    public const string ColaNotificacionesReintento = "notificaciones.reintento";
    public const string ColaNotificacionesMuertos = "notificaciones.muertos";

    public static async Task DeclararAsync(IChannel canal, int reintentoSegundos, CancellationToken ct = default)
    {
        await canal.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(ExchangeReintento, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await DeclararColaConReintentoAsync(canal, ColaCarga, RkCarga, RkCargaMuerto, ColaCargaReintento, ColaCargaMuertos, reintentoSegundos, ct);
        await DeclararColaConReintentoAsync(canal, ColaNotificaciones, RkNotificacion, RkNotificacionMuerto, ColaNotificacionesReintento, ColaNotificacionesMuertos, reintentoSegundos, ct);
    }

    /// <summary>
    /// Declara la cola principal, la cola con TTL para reintentos y la cola de muertos.
    /// El consumidor decide cuándo detener el ciclo según <c>x-death</c>.
    /// </summary>
    private static async Task DeclararColaConReintentoAsync(
        IChannel canal, string cola, string routingKey, string routingKeyMuerto,
        string colaReintento, string colaMuertos, int reintentoSegundos, CancellationToken ct)
    {
        await canal.QueueDeclareAsync(cola, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = ExchangeReintento,
                ["x-dead-letter-routing-key"] = routingKey
            }, cancellationToken: ct);
        await canal.QueueBindAsync(cola, Exchange, routingKey, cancellationToken: ct);

        await canal.QueueDeclareAsync(colaReintento, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = reintentoSegundos * 1000,
                ["x-dead-letter-exchange"] = Exchange,
                ["x-dead-letter-routing-key"] = routingKey
            }, cancellationToken: ct);
        await canal.QueueBindAsync(colaReintento, ExchangeReintento, routingKey, cancellationToken: ct);

        await canal.QueueDeclareAsync(colaMuertos, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(colaMuertos, Exchange, routingKeyMuerto, cancellationToken: ct);
    }

    /// <summary>Lee los reintentos de la cola indicada desde el encabezado <c>x-death</c>.</summary>
    public static int ContarIntentosPrevios(IDictionary<string, object?>? headers, string cola)
    {
        if (headers is null || !headers.TryGetValue("x-death", out var valor) || valor is not IList<object?> muertes)
            return 0;

        return muertes
            .OfType<IDictionary<string, object?>>()
            .Where(m => m.TryGetValue("queue", out var q) && TextoDe(q) == cola)
            .Select(m => m.TryGetValue("count", out var c) && c is long n ? (int)n : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string? TextoDe(object? valor) => valor switch
    {
        byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
        string s => s,
        _ => null
    };
}

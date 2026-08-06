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

    /// <summary>
    /// Sin valores por defecto para host/usuario/password: "rabbitmq"/"guest"/"guest"
    /// siempre funcionaban en docker-compose (.env los inyecta), y por eso mismo
    /// escondían el error si algún día un servicio corre suelto sin configurar nada
    /// — guest/guest en particular es la credencial insegura de fábrica de RabbitMQ.
    /// </summary>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new InvalidOperationException("Falta RabbitMq:Host.");
        if (string.IsNullOrWhiteSpace(Usuario)) throw new InvalidOperationException("Falta RabbitMq:Usuario.");
        if (string.IsNullOrWhiteSpace(Password)) throw new InvalidOperationException("Falta RabbitMq:Password.");
    }
}

/// <summary>
/// Un exchange topic y dos colas — el mínimo que el enunciado evalúa — más el
/// circuito de reintento. Se declara desde los tres servicios: es idempotente,
/// y así ninguno depende de que otro haya arrancado antes.
/// </summary>
public static class Topologia
{
    public const string Exchange = "cargas";
    public const string ExchangeReintento = "cargas.reintento";

    public const string RkCarga = "carga.masiva";
    public const string RkNotificacion = "carga.notificacion";
    public const string RkMuerto = "carga.masiva.muerto";

    public const string ColaCarga = "carga_masiva";
    public const string ColaNotificaciones = "notificaciones";
    public const string ColaReintento = "carga_masiva.reintento";
    public const string ColaMuertos = "carga_masiva.muertos";

    public static async Task DeclararAsync(IChannel canal, int reintentoSegundos, CancellationToken ct = default)
    {
        await canal.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(ExchangeReintento, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        // Un nack sin requeue manda el mensaje al exchange de reintento…
        await canal.QueueDeclareAsync(ColaCarga, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = ExchangeReintento,
                ["x-dead-letter-routing-key"] = RkCarga
            }, cancellationToken: ct);
        await canal.QueueBindAsync(ColaCarga, Exchange, RkCarga, cancellationToken: ct);

        // …donde espera su TTL sin consumidor y vuelve solo a la cola principal.
        // Es el retardo entre reintentos sin plugins ni temporizadores propios.
        await canal.QueueDeclareAsync(ColaReintento, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = reintentoSegundos * 1000,
                ["x-dead-letter-exchange"] = Exchange,
                ["x-dead-letter-routing-key"] = RkCarga
            }, cancellationToken: ct);
        await canal.QueueBindAsync(ColaReintento, ExchangeReintento, RkCarga, cancellationToken: ct);

        // Después de N intentos el consumidor publica acá y el mensaje deja de girar.
        await canal.QueueDeclareAsync(ColaMuertos, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(ColaMuertos, Exchange, RkMuerto, cancellationToken: ct);

        await canal.QueueDeclareAsync(ColaNotificaciones, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(ColaNotificaciones, Exchange, RkNotificacion, cancellationToken: ct);
    }
}

/// <summary>
/// Contratos literales del enunciado (§2️⃣ y §3️⃣). El <c>CorrelationId</c> no se
/// mete acá: viaja como cabecera AMQP para no alterar el JSON que el enunciado
/// define palabra por palabra (design.md §M2).
/// </summary>
public sealed record MensajeCarga(int IdCarga, string RutaArchivo, string Usuario);

public sealed record MensajeNotificacion(int IdCarga, string Usuario, DateTimeOffset FechaFin);

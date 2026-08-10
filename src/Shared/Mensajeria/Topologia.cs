using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks;
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
    /// <c>ExcepcionDeConfiguracion</c> (no <c>InvalidOperationException</c> pelada):
    /// esta validación corre en <c>PublicadorRabbit.CanalAsync</c>, alcanzable
    /// dentro de un request HTTP en curso (clasificacion-excepciones-config).
    /// </summary>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new ExcepcionDeConfiguracion("Falta RabbitMq:Host.");
        if (string.IsNullOrWhiteSpace(Usuario)) throw new ExcepcionDeConfiguracion("Falta RabbitMq:Usuario.");
        if (string.IsNullOrWhiteSpace(Password)) throw new ExcepcionDeConfiguracion("Falta RabbitMq:Password.");
    }
}

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
    /// Un nack sin requeue manda el mensaje al exchange de reintento, donde espera
    /// su TTL sin consumidor y vuelve solo a la cola principal — el retardo entre
    /// reintentos sin plugins ni temporizadores propios. Tras N intentos (el
    /// consumidor cuenta con el header x-death que RabbitMQ ya agrega en cada
    /// vuelta) se publica a mano en la cola de muertos correspondiente y el
    /// mensaje deja de girar.
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

    /// <summary>
    /// RabbitMQ agrega el header "x-death" (arreglo de tablas AMQP) cada vez que un
    /// mensaje muere hacia un dead-letter-exchange. Se busca la entrada de LA COLA
    /// dada y se lee su "count" — la forma estándar de contar reintentos sin
    /// mantener estado propio ni un plugin adicional. Compartido por los dos
    /// consumidores (carga y notificaciones).
    /// </summary>
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

/// <summary>
/// Contratos literales del enunciado (§2️⃣ y §3️⃣). El <c>CorrelationId</c> no se
/// mete acá: viaja como cabecera AMQP para no alterar el JSON que el enunciado
/// define palabra por palabra (design.md §M2).
/// </summary>
public sealed record MensajeCarga(int IdCarga, string RutaArchivo, string Usuario);

public sealed record MensajeNotificacion(
    int IdCarga, string Usuario,
    [property: JsonConverter(typeof(FechaFinJsonConverter))] DateTimeOffset FechaFin);

/// <summary>
/// El ejemplo del enunciado (§3️⃣) es literal: <c>"2025-02-10T10:20:00"</c> — sin
/// offset ni decimales. El converter por defecto de DateTimeOffset sí los agrega
/// (formato "round-trip"); sin este converter, el campo tiene el nombre correcto
/// pero no el formato exacto que matriz-requisitos.md marca como obligatorio (§3.3g).
/// </summary>
internal sealed class FechaFinJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Formato = "yyyy-MM-ddTHH:mm:ss";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString(Formato, CultureInfo.InvariantCulture));
}

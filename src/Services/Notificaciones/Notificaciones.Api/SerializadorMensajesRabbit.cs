using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notificaciones.Api;

/// <summary>Decodifica el contrato RabbitMQ sin introducir detalles JSON en BuildingBlocks.</summary>
internal static class SerializadorMensajesRabbit
{
    public static JsonSerializerOptions CrearOpciones()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new FechaFinJsonConverter());
        return opciones;
    }

    private sealed class FechaFinJsonConverter : JsonConverter<DateTimeOffset>
    {
        private const string Formato = "yyyy-MM-ddTHH:mm:ss";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString(Formato, CultureInfo.InvariantCulture));
    }
}
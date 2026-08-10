using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks;

/// <summary>
/// Contratos de integración independientes del transporte. El correlationId viaja
/// como metadata del adaptador, no como parte del cuerpo del mensaje.
/// </summary>
public sealed record MensajeCarga(int IdCarga, string RutaArchivo, string Usuario);

public sealed record MensajeNotificacion(
    int IdCarga, string Usuario,
    [property: JsonConverter(typeof(FechaFinJsonConverter))] DateTimeOffset FechaFin);

internal sealed class FechaFinJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Formato = "yyyy-MM-ddTHH:mm:ss";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString(Formato, CultureInfo.InvariantCulture));
}

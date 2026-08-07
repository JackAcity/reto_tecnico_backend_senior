using System.Text.Json;
using Mensajeria;

namespace CargaMasiva.Tests;

/// <summary>
/// §3.3g/matriz-requisitos.md: el enunciado da el JSON exacto de cada mensaje.
/// Los nombres de campo ya salían bien por camelCase; el valor de fechaFin no —
/// DateTimeOffset por defecto agrega offset y decimales
/// ("2026-08-07T00:48:09.767+00:00"), el enunciado da "2025-02-10T10:20:00".
/// </summary>
public class ContratoMensajesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MensajeCarga_SerializaConLosNombresLiteralesDelEnunciado()
    {
        var json = JsonSerializer.Serialize(new MensajeCarga(123, "seaweed://.../archivo.xlsx", "user@example.com"), Json);

        Assert.Equal(
            """{"idCarga":123,"rutaArchivo":"seaweed://.../archivo.xlsx","usuario":"user@example.com"}""",
            json);
    }

    [Fact]
    public void MensajeNotificacion_SerializaFechaFinSinOffsetNiDecimales()
    {
        var fecha = new DateTimeOffset(2025, 2, 10, 10, 20, 0, TimeSpan.Zero);

        var json = JsonSerializer.Serialize(new MensajeNotificacion(123, "user@example.com", fecha), Json);

        Assert.Equal(
            """{"idCarga":123,"usuario":"user@example.com","fechaFin":"2025-02-10T10:20:00"}""",
            json);
    }

    [Fact]
    public void MensajeNotificacion_ConvierteAUtcAntesDeFormatear()
    {
        // Una hora con offset distinto de cero también debe caer en el mismo
        // formato — se normaliza a UTC, no se trunca el offset sin más.
        var fechaLimaMenos5 = new DateTimeOffset(2025, 2, 10, 5, 20, 0, TimeSpan.FromHours(-5));

        var json = JsonSerializer.Serialize(new MensajeNotificacion(123, "user@example.com", fechaLimaMenos5), Json);

        Assert.Contains("\"fechaFin\":\"2025-02-10T10:20:00\"", json);
    }

    [Fact]
    public void MensajeNotificacion_RoundTrip_DeserializaCorrectamente()
    {
        var original = new MensajeNotificacion(123, "user@example.com", new DateTimeOffset(2025, 2, 10, 10, 20, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(original, Json);
        var recuperado = JsonSerializer.Deserialize<MensajeNotificacion>(json, Json);

        Assert.Equal(original.IdCarga, recuperado!.IdCarga);
        Assert.Equal(original.Usuario, recuperado.Usuario);
        Assert.Equal(original.FechaFin, recuperado.FechaFin);
    }
}

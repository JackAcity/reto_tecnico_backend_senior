using System.Text.Json;
using BuildingBlocks;
using ServiceHost;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Reto.Tests;

/// <summary>
/// §4.6 — manejo de excepciones global. Prueba directa contra <see cref="GlobalExceptionHandler"/>
/// (sin levantar un host HTTP): confirma que un 500 no filtra <c>ex.Message</c> al cliente
/// (podría traer detalles de Npgsql/RabbitMQ), y que los tipos esperados sí lo exponen.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<JsonElement> ManejarAsync(Exception ex)
    {
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        var manejador = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        await manejador.TryHandleAsync(http, ex, CancellationToken.None);

        http.Response.Body.Position = 0;
        using var lector = new StreamReader(http.Response.Body);
        return JsonDocument.Parse(await lector.ReadToEndAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task ExcepcionNoClasificada_Da500_SinFiltrarElMensajeInterno()
    {
        var json = await ManejarAsync(new NpgsqlFake("Host=db-interno;Password=secreto123;"));

        Assert.DoesNotContain("secreto123", json.GetProperty("detail").GetString());
        Assert.DoesNotContain("db-interno", json.GetProperty("detail").GetString());
        Assert.Equal(StatusCodes.Status500InternalServerError, json.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status403Forbidden)]
    [InlineData(typeof(KeyNotFoundException), StatusCodes.Status404NotFound)]
    public async Task ExcepcionEsperada_SiExponeSuMensaje(Type tipo, int statusEsperado)
    {
        var ex = (Exception)Activator.CreateInstance(tipo, "mensaje pensado para el cliente")!;

        var json = await ManejarAsync(ex);

        Assert.Equal("mensaje pensado para el cliente", json.GetProperty("detail").GetString());
        Assert.Equal(statusEsperado, json.GetProperty("status").GetInt32());
    }

    /// <summary>
    /// clasificacion-excepciones-config: ExcepcionDeConfiguracion es una
    /// InvalidOperationException (mismo tipo base que TransicionInvalidaException,
    /// que sí es 400 arriba), pero su mensaje es el nombre de una variable de
    /// entorno faltante (Topologia.Validar, ServicioAutenticacion.LlaveDeFirma),
    /// no algo pensado para el cliente — debe ganarle al case combinado y caer
    /// en 500 sin fuga. Reproduce el hallazgo real de auditoría.
    /// </summary>
    [Fact]
    public async Task ExcepcionDeConfiguracion_Da500_SinFiltrarElMensaje()
    {
        var json = await ManejarAsync(new ExcepcionDeConfiguracion("Falta RabbitMq:Password."));

        Assert.Equal(StatusCodes.Status500InternalServerError, json.GetProperty("status").GetInt32());
        var detalle = json.GetProperty("detail").GetString();
        Assert.DoesNotContain("RabbitMq", detalle);
        Assert.DoesNotContain("Password", detalle);
    }

    /// <summary>TransicionInvalidaException real (no genérica): confirma que su contrato 400 no cambió al introducir ExcepcionDeConfiguracion.</summary>
    [Fact]
    public async Task TransicionInvalidaException_Real_Da400_ConSuMensaje()
    {
        var json = await ManejarAsync(new CargaMasiva.Domain.TransicionInvalidaException(
            CargaMasiva.Domain.EstadoCarga.Finalizado, CargaMasiva.Domain.EstadoCarga.Pendiente));

        Assert.Equal(StatusCodes.Status400BadRequest, json.GetProperty("status").GetInt32());
        Assert.Contains("Transición inválida", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SiempreIncluyeCorrelationId_ParaCorrelacionarConElLogDelServidor()
    {
        var json = await ManejarAsync(new InvalidOperationException("x"));

        Assert.True(json.TryGetProperty("correlationId", out _));
    }

    /// <summary>Simula una excepción de infraestructura (Npgsql, RabbitMQ.Client) sin depender de esos paquetes acá.</summary>
    private sealed class NpgsqlFake(string message) : Exception(message);
}

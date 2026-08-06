using System.Text.Json;
using BuildingBlocks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CargaMasiva.Tests;

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

    [Fact]
    public async Task SiempreIncluyeCorrelationId_ParaCorrelacionarConElLogDelServidor()
    {
        var json = await ManejarAsync(new InvalidOperationException("x"));

        Assert.True(json.TryGetProperty("correlationId", out _));
    }

    /// <summary>Simula una excepción de infraestructura (Npgsql, RabbitMQ.Client) sin depender de esos paquetes acá.</summary>
    private sealed class NpgsqlFake(string message) : Exception(message);
}

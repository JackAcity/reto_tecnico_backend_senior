using Mensajeria;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Reto.Tests;

/// <summary>
/// Integración real contra RabbitMQ. Requiere el stack levantado
/// (<c>docker compose up -d rabbitmq</c>).
/// </summary>
public sealed class PublicadorRabbitTests : IAsyncLifetime
{
    private sealed class LoggerEspia<T> : ILogger<T>
    {
        public List<(LogLevel Nivel, string Mensaje)> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entradas.Add((logLevel, formatter(state, exception)));
    }

    private readonly LoggerEspia<PublicadorRabbit> _logger = new();
    private PublicadorRabbit _publicador = null!;

    public Task InitializeAsync()
    {
        var opciones = Options.Create(new OpcionesRabbit
        {
            Host = Environment.GetEnvironmentVariable("RabbitMq__Host") ?? "localhost",
            Usuario = Environment.GetEnvironmentVariable("RabbitMq__Usuario") ?? "reto",
            Password = Environment.GetEnvironmentVariable("RabbitMq__Password") ?? "cambiar_en_local"
        });
        _publicador = new PublicadorRabbit(opciones, _logger);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _publicador.DisposeAsync();

    /// <summary>
    /// Comportamiento real de RabbitMQ.Client 7.2.2 (no una suposición): con
    /// publisherConfirmationTrackingEnabled, un routing key sin cola vinculada NO
    /// se pierde en silencio — el cliente lanza PublishReturnException. El cliente
    /// técnico la normaliza a FalloPublicacionRabbitException para que el adaptador
    /// local de cada caso de uso la traduzca a su Resultado.
    /// </summary>
    [Fact]
    public async Task RoutingKeySinColaVinculada_HaceFallarLaPublicacion()
    {
        var exception = await Assert.ThrowsAsync<FalloPublicacionRabbitException>(() =>
            _publicador.PublicarAsync("ruta.que.no.existe.en.ningun.binding", new { x = 1 }, "test-return"));

        Assert.Contains("ruta.que.no.existe.en.ningun.binding", exception.Message);
    }

    /// <summary>
    /// El log de AlRetornarMensajeAsync es diagnóstico adicional, no la red de
    /// seguridad — la excepción de arriba lo es. El basic.return es un evento
    /// del canal separado del fallo del confirm, así que se sondea con límite
    /// acotado en vez de asumir un orden estricto entre ambos.
    /// </summary>
    [Fact]
    public async Task RoutingKeySinColaVinculada_TambienQuedaLogueadaConCamposEstructurados()
    {
        await Assert.ThrowsAsync<FalloPublicacionRabbitException>(() =>
            _publicador.PublicarAsync("otra.ruta.sin.binding", new { x = 1 }, "test-return"));

        var limite = DateTime.UtcNow.AddSeconds(5);
        while (_logger.Entradas.Count == 0 && DateTime.UtcNow < limite)
            await Task.Delay(100);

        var entrada = Assert.Single(_logger.Entradas);
        Assert.Equal(LogLevel.Error, entrada.Nivel);
        Assert.Contains("otra.ruta.sin.binding", entrada.Mensaje);
    }
}

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;

namespace BuildingBlocks;

/// <summary>
/// Configuración transversal compartida por los cinco servicios.
/// Cubre los requisitos obligatorios §4.6 (manejo de excepciones global)
/// y §4.7 (logging estructurado) sin duplicarlos en cada Program.cs.
/// </summary>
public static class ServiceDefaults
{
    public const string CorrelationHeader = "X-Correlation-Id";

    // El CorrelationId de un request entrante lo escribe el CLIENTE (o el gateway
    // reenviando el del cliente). Sin validar, ese string cae directo en el log de
    // consola vía Serilog — un valor con saltos de línea permite inyectar líneas de
    // log falsas ("log injection"); uno larguísimo infla el almacenamiento de logs.
    // Se acepta solo lo que un GUID/hex/timestamp legítimo produciría; cualquier
    // otra cosa se descarta y se genera uno nuevo, en vez de confiar a ciegas en
    // input del cliente.
    private static readonly Regex CorrelationIdValido = new("^[a-zA-Z0-9-]{1,64}$", RegexOptions.Compiled);

    /// <summary>Pública para poder probarla directo, sin levantar un host HTTP.</summary>
    public static string CorrelationIdSeguro(string? entrante) =>
        entrante is not null && CorrelationIdValido.IsMatch(entrante)
            ? entrante
            : Guid.NewGuid().ToString("N");

    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((ctx, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Servicio", serviceName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Servicio}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddHealthChecks();

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app, string serviceName)
    {
        app.UseExceptionHandler();

        // Único header de seguridad que rinde acá: los 5 servicios son APIs JSON
        // puras, sin vistas HTML que un navegador renderice, así que CSP/X-Frame-
        // Options no protegen nada real (no hay página que clickjackear ni script
        // que inyectar). nosniff sí es universal y gratis: evita que un cliente
        // que decida "oler" el content-type reinterprete una respuesta JSON como
        // algo ejecutable.
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.XContentTypeOptions = "nosniff";
            await next();
        });

        // El CorrelationId entra por header HTTP y se propaga al log de todo el request.
        // Hacia RabbitMQ viaja como header AMQP, para no alterar el contrato de mensaje
        // que el enunciado define de forma literal (ver design.md §M2).
        app.Use(async (ctx, next) =>
        {
            var correlationId = CorrelationIdSeguro(ctx.Request.Headers[CorrelationHeader].FirstOrDefault());
            ctx.Response.Headers[CorrelationHeader] = correlationId;
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });

        app.UseSerilogRequestLogging();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { servicio = serviceName, estado = "activo" }));

        return app;
    }
}

/// <summary>
/// Requisito §4.6 — manejo de excepciones global, sin try/catch por endpoint.
/// Pública (no internal) para poder probarla directamente, sin levantar un host HTTP.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Excepción no controlada en {Ruta}", http.Request.Path);

        // ex.Message solo se expone para las excepciones esperadas (mensajes que el
        // propio código escribió pensando en que el cliente los lea, ej.
        // TransicionInvalidaException). ExcepcionDeConfiguracion se matchea ANTES
        // que el case combinado: es una InvalidOperationException, pero su mensaje
        // (nombre de una variable de entorno faltante — Falta RabbitMq:Host, Falta
        // Jwt:Key) no está pensado para el cliente, aunque comparta tipo base con
        // una excepción de negocio real (clasificacion-excepciones-config). Para
        // todo lo demás no clasificado (500: fallo de Npgsql, de RabbitMQ, o
        // cualquier excepción no prevista) el mensaje puede traer detalles
        // internos — connection strings, nombres de columna, stack de terceros.
        // Ahí solo se expone el correlationId; el detalle real queda en el log
        // del servidor, ya escrito arriba.
        var (status, titulo, exponerDetalle) = ex switch
        {
            ExcepcionDeConfiguracion => (StatusCodes.Status500InternalServerError, "Error interno", false),
            ArgumentException or InvalidOperationException => (StatusCodes.Status400BadRequest, "Solicitud inválida", true),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acceso denegado", true),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado", true),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", false)
        };

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new
        {
            title = titulo,
            status,
            detail = exponerDetalle ? ex.Message : "Ocurrió un error inesperado. Usa el correlationId para ubicar el detalle en los logs del servidor.",
            correlationId = http.Response.Headers[ServiceDefaults.CorrelationHeader].FirstOrDefault()
        }, ct);

        return true;
    }
}

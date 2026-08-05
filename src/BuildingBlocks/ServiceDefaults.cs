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

        // El CorrelationId entra por header HTTP y se propaga al log de todo el request.
        // Hacia RabbitMQ viaja como header AMQP, para no alterar el contrato de mensaje
        // que el enunciado define de forma literal (ver design.md §M2).
        app.Use(async (ctx, next) =>
        {
            var correlationId = ctx.Request.Headers[CorrelationHeader].FirstOrDefault()
                                ?? Guid.NewGuid().ToString("N");
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

/// <summary>Requisito §4.6 — manejo de excepciones global, sin try/catch por endpoint.</summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Excepción no controlada en {Ruta}", http.Request.Path);

        var (status, titulo) = ex switch
        {
            ArgumentException or InvalidOperationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acceso denegado"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new
        {
            title = titulo,
            status,
            detail = ex.Message,
            correlationId = http.Response.Headers[ServiceDefaults.CorrelationHeader].FirstOrDefault()
        }, ct);

        return true;
    }
}

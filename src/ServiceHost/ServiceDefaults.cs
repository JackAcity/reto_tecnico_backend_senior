using System.Text.RegularExpressions;
using BuildingBlocks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;

namespace ServiceHost;

/// <summary>
/// Configuración transversal del host HTTP, compartida por los cinco servicios.
/// </summary>
public static class ServiceDefaults
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private static readonly Regex CorrelationIdValido = new("^[a-zA-Z0-9-]{1,64}$", RegexOptions.Compiled);

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

        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.XContentTypeOptions = "nosniff";
            await next();
        });

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

/// <summary>Manejo global de excepciones del borde HTTP.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Excepción no controlada en {Ruta}", http.Request.Path);

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

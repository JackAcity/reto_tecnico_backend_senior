using System.Threading.RateLimiting;
using ServiceHost;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Gateway");

// El transporte deja margen para que Control devuelva el error de negocio, no un 413 del gateway.
var tamanoMaximoMb = builder.Configuration.GetValue("Carga:TamanoMaximoMb", 25);
var limiteTransporte = (tamanoMaximoMb + 1L) * 1024 * 1024;

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteTransporte);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limiteTransporte);

// El gateway es el único borde público y valida el JWT antes de enrutar.
builder.Services.AddAutenticacionJwt(builder.Configuration);

// Sólo el borde público habilita los orígenes explícitos del cliente web.
var origenesPermitidos = (builder.Configuration["Cors:OrigenesPermitidos"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddPolicy("cliente-web", p => p
    .WithOrigins(origenesPermitidos)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// La cuota se reparte por usuario autenticado o por IP para solicitudes anónimas.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    o.AddPolicy(Politicas.LimitePorUsuario, http =>
        RateLimitPartition.GetFixedWindowLimiter(Politicas.Particion(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1)
        }));

    // Las cargas consumen archivo, cola y procesamiento; necesitan una cuota menor.
    o.AddPolicy(Politicas.LimiteCarga, http =>
        RateLimitPartition.GetFixedWindowLimiter(Politicas.Particion(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        }));

    // El login es anónimo; la cuota por IP limita intentos de fuerza bruta.
    o.AddPolicy(Politicas.LimiteLogin, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    o.OnRejected = async (contexto, ct) =>
    {
        contexto.HttpContext.Response.Headers.RetryAfter = "60";
        await contexto.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Demasiadas solicitudes",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Se superó el límite de peticiones. Reintentar en un minuto."
        }, ct);
    };
});

// Las rutas reutilizan el mismo límite calculado para evitar techos inconsistentes.
builder.Services
    .AddReverseProxy()
    .LoadFromMemory(Politicas.Rutas(limiteTransporte), Politicas.Clusters(builder.Configuration));

var app = builder.Build();
app.UseServiceDefaults("Gateway");

app.UseCors("cliente-web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();

/// <summary>Políticas, rutas y destinos del borde público.</summary>
internal static class Politicas
{
    public const string LimitePorUsuario = "porUsuario";
    public const string LimiteCarga = "carga";
    public const string LimiteLogin = "login";

    /// <summary>Usa el usuario autenticado o la IP como clave de cuota.</summary>
    public static string Particion(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "anonimo";

    // Login y refresh no deben heredar el límite de las cargas de archivos.
    private const long LimiteCuerpoAuth = 4 * 1024;

    public static IReadOnlyList<RouteConfig> Rutas(long limiteTransporte) =>
    [
        new RouteConfig
        {
            RouteId = "auth-login",
            ClusterId = "auth",
            Match = new RouteMatch { Path = "/auth/login" },
            RateLimiterPolicy = LimiteLogin,
            MaxRequestBodySize = LimiteCuerpoAuth
        },
        new RouteConfig
        {
            RouteId = "auth-refresh",
            ClusterId = "auth",
            Match = new RouteMatch { Path = "/auth/refresh" },
            RateLimiterPolicy = LimiteLogin,
            MaxRequestBodySize = LimiteCuerpoAuth
        },
        // Subir y consultar requieren permisos y cuotas diferentes.
        new RouteConfig
        {
            RouteId = "cargas-subida",
            ClusterId = "control",
            Match = new RouteMatch { Path = "/cargas", Methods = ["POST"] },
            AuthorizationPolicy = Autenticacion.PoliticaCargaMasiva,
            RateLimiterPolicy = LimiteCarga,
            MaxRequestBodySize = limiteTransporte
        },
        // El historial usa la cuota de lectura para no penalizar el polling del cliente.
        new RouteConfig
        {
            RouteId = "cargas-consulta",
            ClusterId = "control",
            Match = new RouteMatch { Path = "/cargas/{**resto}", Methods = ["GET"] },
            AuthorizationPolicy = Autenticacion.PoliticaAutenticado,
            RateLimiterPolicy = LimitePorUsuario
        },
        new RouteConfig
        {
            RouteId = "cargamasiva",
            ClusterId = "cargamasiva",
            Match = new RouteMatch { Path = "/servicios/cargamasiva/{**resto}" },
            AuthorizationPolicy = Autenticacion.PoliticaAutenticado,
            RateLimiterPolicy = LimitePorUsuario,
            Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = "/servicios/cargamasiva" }]
        },
        new RouteConfig
        {
            RouteId = "notificaciones",
            ClusterId = "notificaciones",
            Match = new RouteMatch { Path = "/servicios/notificaciones/{**resto}" },
            AuthorizationPolicy = Autenticacion.PoliticaAutenticado,
            RateLimiterPolicy = LimitePorUsuario,
            Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = "/servicios/notificaciones" }]
        }
    ];

    public static IReadOnlyList<ClusterConfig> Clusters(IConfiguration config) =>
    [
        Cluster("auth", Requerido(config, "Servicios:Auth")),
        Cluster("control", Requerido(config, "Servicios:Control")),
        Cluster("cargamasiva", Requerido(config, "Servicios:CargaMasiva")),
        Cluster("notificaciones", Requerido(config, "Servicios:Notificaciones"))
    ];

    private static string Requerido(IConfiguration config, string clave) =>
        config[clave] ?? throw new InvalidOperationException($"Falta {clave}.");

    private static ClusterConfig Cluster(string id, string direccion) => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["destino"] = new DestinationConfig { Address = direccion }
        }
    };
}

using System.Text;
using System.Threading.RateLimiting;
using BuildingBlocks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Gateway");

// ---------------------------------------------------------------------------
// §C12 — el tamaño máximo tiene TRES techos distintos (Kestrel, form options y
// YARP). Si no se configuran los tres, el usuario recibe un 413 crudo del
// gateway que parece un bug. Se define una sola vez, aquí.
// El transporte permite 1 MB más que el límite de negocio: así Control alcanza
// a responder un error claro en vez de que la conexión se corte antes.
// ---------------------------------------------------------------------------
var tamanoMaximoMb = builder.Configuration.GetValue("Carga:TamanoMaximoMb", 25);
var limiteTransporte = (tamanoMaximoMb + 1L) * 1024 * 1024;

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteTransporte);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limiteTransporte);

// ---------------------------------------------------------------------------
// Validación del JWT en el borde. Los microservicios de atrás no publican
// puertos (ver docker-compose.yml), así que esta es la única entrada.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Sin el mapeo heredado, los claims conservan su nombre original ("sub",
        // "role", "permiso") y el particionado del rate limiter puede leerlos.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Falta Jwt:Key."))),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("autenticado", p => p.RequireAuthenticatedUser())
    // §3.2a — no basta con estar autenticado: hay que tener el permiso de carga.
    .AddPolicy("cargaMasiva", p => p
        .RequireAuthenticatedUser()
        .RequireClaim(Politicas.ClaimPermiso, Politicas.PermisoCargaMasiva));

// ---------------------------------------------------------------------------
// Rate limiting (obligatorio). Particionado por `sub`: un usuario no consume la
// cuota de otro. Sin token, la partición cae al IP de origen.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    o.AddPolicy(Politicas.LimitePorUsuario, http =>
        RateLimitPartition.GetFixedWindowLimiter(Politicas.Particion(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1)
        }));

    // La carga es cara (archivo + cola + procesamiento): cuota propia y más baja.
    o.AddPolicy(Politicas.LimiteCarga, http =>
        RateLimitPartition.GetFixedWindowLimiter(Politicas.Particion(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        }));

    // El login es anónimo por definición: se particiona por IP para que no sirva
    // de oráculo de fuerza bruta.
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

// Rutas en código y no en appsettings: el límite de tamaño por ruta tiene que
// salir del mismo cálculo que Kestrel y form options (§C12). Duplicar el número
// en un JSON es la forma habitual de que los tres techos dejen de coincidir.
builder.Services
    .AddReverseProxy()
    .LoadFromMemory(Politicas.Rutas(limiteTransporte), Politicas.Clusters(builder.Configuration));

var app = builder.Build();
app.UseServiceDefaults("Gateway");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();

/// <summary>Configuración del borde en un solo lugar: políticas, rutas y destinos.</summary>
internal static class Politicas
{
    public const string ClaimPermiso = "permiso";
    public const string PermisoCargaMasiva = "carga:masiva";
    public const string LimitePorUsuario = "porUsuario";
    public const string LimiteCarga = "carga";
    public const string LimiteLogin = "login";

    /// <summary>Clave de partición del rate limiter: el usuario del token, o el IP si es anónimo.</summary>
    public static string Particion(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "anonimo";

    public static IReadOnlyList<RouteConfig> Rutas(long limiteTransporte) =>
    [
        // Anónimas por necesidad: sin login no hay token.
        new RouteConfig
        {
            RouteId = "auth-login",
            ClusterId = "auth",
            Match = new RouteMatch { Path = "/auth/login" },
            RateLimiterPolicy = LimiteLogin
        },
        new RouteConfig
        {
            RouteId = "auth-refresh",
            ClusterId = "auth",
            Match = new RouteMatch { Path = "/auth/refresh" },
            RateLimiterPolicy = LimiteLogin
        },
        // La subida: exige el permiso de carga y trae su propio techo de tamaño.
        new RouteConfig
        {
            RouteId = "cargas",
            ClusterId = "control",
            Match = new RouteMatch { Path = "/cargas/{**resto}" },
            AuthorizationPolicy = "cargaMasiva",
            RateLimiterPolicy = LimiteCarga,
            MaxRequestBodySize = limiteTransporte
        },
        // Diagnóstico de los servicios internos por la única puerta pública.
        new RouteConfig
        {
            RouteId = "cargamasiva",
            ClusterId = "cargamasiva",
            Match = new RouteMatch { Path = "/servicios/cargamasiva/{**resto}" },
            AuthorizationPolicy = "autenticado",
            RateLimiterPolicy = LimitePorUsuario,
            Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = "/servicios/cargamasiva" }]
        },
        new RouteConfig
        {
            RouteId = "notificaciones",
            ClusterId = "notificaciones",
            Match = new RouteMatch { Path = "/servicios/notificaciones/{**resto}" },
            AuthorizationPolicy = "autenticado",
            RateLimiterPolicy = LimitePorUsuario,
            Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = "/servicios/notificaciones" }]
        }
    ];

    public static IReadOnlyList<ClusterConfig> Clusters(IConfiguration config) =>
    [
        Cluster("auth", config["Servicios:Auth"] ?? "http://auth:8080/"),
        Cluster("control", config["Servicios:Control"] ?? "http://control:8080/"),
        Cluster("cargamasiva", config["Servicios:CargaMasiva"] ?? "http://cargamasiva:8080/"),
        Cluster("notificaciones", config["Servicios:Notificaciones"] ?? "http://notificaciones:8080/")
    ];

    private static ClusterConfig Cluster(string id, string direccion) => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["destino"] = new DestinationConfig { Address = direccion }
        }
    };
}

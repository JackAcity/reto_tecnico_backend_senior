using System.Threading.RateLimiting;
using ServiceHost;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
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

// Validación del JWT en el borde, con la misma configuración que usa Control
// (BuildingBlocks.Autenticacion). Los microservicios de atrás no publican
// puertos (ver docker-compose.yml), así que esta es la única entrada pública.
builder.Services.AddAutenticacionJwt(builder.Configuration);

// ---------------------------------------------------------------------------
// CORS — solo acá: Gateway es la única puerta pública. El cliente React (Vite
// dev, distinto puerto) está sujeto a same-origin policy y necesita el header
// explícito; Postman no (no corre en un navegador). Nunca AllowAnyOrigin, y sin
// AllowCredentials porque el JWT viaja en Authorization, no en cookies.
// ---------------------------------------------------------------------------
var origenesPermitidos = (builder.Configuration["Cors:OrigenesPermitidos"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddPolicy("cliente-web", p => p
    .WithOrigins(origenesPermitidos)
    .AllowAnyHeader()
    .AllowAnyMethod()));

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

app.UseCors("cliente-web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();

/// <summary>Configuración del borde en un solo lugar: políticas, rutas y destinos.</summary>
internal static class Politicas
{
    public const string LimitePorUsuario = "porUsuario";
    public const string LimiteCarga = "carga";
    public const string LimiteLogin = "login";

    /// <summary>Clave de partición del rate limiter: el usuario del token, o el IP si es anónimo.</summary>
    public static string Particion(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "anonimo";

    // Kestrel.MaxRequestBodySize es un techo por PROCESO (limiteTransporte, ~26 MB,
    // dimensionado para la subida) — sin este override, /auth/login hereda ese
    // mismo techo aunque un login/refresh nunca pese más de un par de KB. YARP
    // permite un límite MENOR por ruta, que es justo lo que hace falta acá: no
    // gastar tiempo de Kestrel leyendo hasta 26 MB de un POST que iba a fallar la
    // validación de todos modos.
    private const long LimiteCuerpoAuth = 4 * 1024;

    public static IReadOnlyList<RouteConfig> Rutas(long limiteTransporte) =>
    [
        // Anónimas por necesidad: sin login no hay token.
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
        // La subida: exige el permiso de carga y trae su propio techo de tamaño.
        // Separada de la consulta (abajo) — antes una sola ruta exigía PoliticaCargaMasiva
        // para TODO /cargas/{**resto}, así que el rol "consulta" (sin ese permiso, pero
        // sí autenticado) recibía 403 al pedir su propio historial, aunque Control ya
        // exige solo PoliticaAutenticado en sus GET (Control.Api/Program.cs). Encontrado
        // al probar el cliente React con el usuario consulta@reto.local.
        new RouteConfig
        {
            RouteId = "cargas-subida",
            ClusterId = "control",
            Match = new RouteMatch { Path = "/cargas", Methods = ["POST"] },
            AuthorizationPolicy = Autenticacion.PoliticaCargaMasiva,
            RateLimiterPolicy = LimiteCarga,
            MaxRequestBodySize = limiteTransporte
        },
        // Historial, detalle y descarga: cualquier usuario autenticado, igual que Control.
        // Con LimitePorUsuario (60/min) y no LimiteCarga (10/min) — el polling del
        // cliente web (cada 3 s) supera las 10/min de la cuota de subida.
        new RouteConfig
        {
            RouteId = "cargas-consulta",
            ClusterId = "control",
            Match = new RouteMatch { Path = "/cargas/{**resto}", Methods = ["GET"] },
            AuthorizationPolicy = Autenticacion.PoliticaAutenticado,
            RateLimiterPolicy = LimitePorUsuario
        },
        // Diagnóstico de los servicios internos por la única puerta pública.
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

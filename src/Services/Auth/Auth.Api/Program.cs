using Auth.Api;
using BuildingBlocks;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Auth");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.Configure<OpcionesJwt>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<ServicioAutenticacion>();

var app = builder.Build();
app.UseServiceDefaults("Auth");

// El esquema ya existe: Auth espera a que Control termine de migrar (ver depends_on
// en el compose, design.md §C11). Aquí solo se siembra el usuario de demo, sin el
// cual no hay forma de obtener un JWT en una base recién creada.
await using (var alcance = app.Services.CreateAsyncScope())
{
    var db = alcance.ServiceProvider.GetRequiredService<RetoDbContext>();

    // Nada de esto tiene fallback en código — ni siquiera Email/Rol, que no son
    // secretos pero SÍ son privilegio. Un default silencioso de Rol="administrador"
    // es peor que uno de Password: si Seed:Rol faltara, el sistema crearía en
    // silencio un usuario con el privilegio MÁXIMO — fail-open hacia arriba,
    // exactamente lo contrario de mínimo privilegio. Todo falla al arrancar si
    // falta, sin adivinar ni la identidad ni el rol.
    var creado = await db.SembrarUsuarioAsync(
        Requerido(builder.Configuration["Seed:Email"], "Seed:Email"),
        Requerido(builder.Configuration["Seed:Password"], "Seed:Password"),
        Requerido(builder.Configuration["Seed:Rol"], "Seed:Rol"));
    app.Logger.LogInformation("Usuario semilla (admin) {Estado}", creado ? "creado" : "ya existía");

    // Sin esto, el único usuario del sistema es un administrador — no hay forma
    // de demostrar en vivo que la policy cargaMasiva realmente RECHAZA a un
    // usuario autenticado sin el permiso (PermisosDe ya lo cubre en unit test,
    // pero nunca se probó de punta a punta con una cuenta real). Rol "consulta":
    // PermisosDe no le asigna carga:masiva.
    var creadoConsulta = await db.SembrarUsuarioAsync(
        Requerido(builder.Configuration["Seed:ConsultaEmail"], "Seed:ConsultaEmail"),
        Requerido(builder.Configuration["Seed:ConsultaPassword"], "Seed:ConsultaPassword"),
        Requerido(builder.Configuration["Seed:ConsultaRol"], "Seed:ConsultaRol"));
    app.Logger.LogInformation("Usuario semilla (consulta) {Estado}", creadoConsulta ? "creado" : "ya existía");
}

// §2.3 — login. El 401 es idéntico para usuario inexistente y contraseña incorrecta:
// distinguirlos permitiría enumerar cuentas válidas.
app.MapPost("/auth/login", async (SolicitudLogin req, ServicioAutenticacion svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["credenciales"] = ["Email y password son obligatorios."]
        });

    var resultado = await svc.LoginAsync(req.Email.Trim(), req.Password, ct);

    return resultado is null
        ? Results.Problem(title: "Credenciales inválidas", statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(resultado);
});

// §2.3d — refresh con rotación: el token entregado se revoca en el mismo movimiento.
app.MapPost("/auth/refresh", async (SolicitudRefresh req, ServicioAutenticacion svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.RefreshToken))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["refreshToken"] = ["El refresh token es obligatorio."]
        });

    var resultado = await svc.RefrescarAsync(req.RefreshToken.Trim(), ct);

    return resultado is null
        ? Results.Problem(title: "Refresh token inválido, expirado o ya usado", statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(resultado);
});

app.Run();

/// <summary>IsNullOrWhiteSpace y no solo null: una variable de entorno declarada vacía no es "ausente".</summary>
static string Requerido(string? valor, string clave) =>
    string.IsNullOrWhiteSpace(valor) ? throw new InvalidOperationException($"Falta {clave}.") : valor;

public sealed record SolicitudLogin(string? Email, string? Password);
public sealed record SolicitudRefresh(string? RefreshToken);

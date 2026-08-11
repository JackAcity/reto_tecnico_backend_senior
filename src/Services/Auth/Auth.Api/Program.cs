using Auth.Api;
using ServiceHost;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Auth");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.Configure<OpcionesJwt>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IRepositorioUsuarios, RepositorioUsuariosEf>();
builder.Services.AddSingleton<IProtectorContrasenas, ProtectorContrasenas>();
builder.Services.AddSingleton<IEmisorAccessToken, EmisorJwt>();
builder.Services.AddScoped<ServicioAutenticacion>();

var app = builder.Build();
app.UseServiceDefaults("Auth");

// Control migra el esquema; Auth sólo siembra las cuentas necesarias para acceder.
await using (var alcance = app.Services.CreateAsyncScope())
{
    var db = alcance.ServiceProvider.GetRequiredService<RetoDbContext>();

    // La identidad y el rol deben ser explícitos; un rol predeterminado podría elevar privilegios.
    var creado = await db.SembrarUsuarioAsync(
        Requerido(builder.Configuration["Seed:Email"], "Seed:Email"),
        Requerido(builder.Configuration["Seed:Password"], "Seed:Password"),
        Requerido(builder.Configuration["Seed:Rol"], "Seed:Rol"));
    app.Logger.LogInformation("Usuario semilla (admin) {Estado}", creado ? "creado" : "ya existía");

    // Esta cuenta permite verificar el acceso autenticado sin permiso de carga.
    var creadoConsulta = await db.SembrarUsuarioAsync(
        Requerido(builder.Configuration["Seed:ConsultaEmail"], "Seed:ConsultaEmail"),
        Requerido(builder.Configuration["Seed:ConsultaPassword"], "Seed:ConsultaPassword"),
        Requerido(builder.Configuration["Seed:ConsultaRol"], "Seed:ConsultaRol"));
    app.Logger.LogInformation("Usuario semilla (consulta) {Estado}", creadoConsulta ? "creado" : "ya existía");
}

// Una respuesta idéntica evita enumerar cuentas válidas.
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

/// <summary>Rechaza valores de configuración ausentes o en blanco.</summary>
static string Requerido(string? valor, string clave) =>
    string.IsNullOrWhiteSpace(valor) ? throw new InvalidOperationException($"Falta {clave}.") : valor;

public sealed record SolicitudLogin(string? Email, string? Password);
public sealed record SolicitudRefresh(string? RefreshToken);

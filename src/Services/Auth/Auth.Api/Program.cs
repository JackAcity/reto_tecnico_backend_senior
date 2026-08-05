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
    var creado = await db.SembrarUsuarioAsync(
        builder.Configuration["Seed:Email"] ?? "admin@reto.local",
        builder.Configuration["Seed:Password"] ?? "Reto2026!",
        builder.Configuration["Seed:Rol"] ?? "administrador");

    app.Logger.LogInformation("Usuario semilla {Estado}", creado ? "creado" : "ya existía");
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

public sealed record SolicitudLogin(string? Email, string? Password);
public sealed record SolicitudRefresh(string? RefreshToken);

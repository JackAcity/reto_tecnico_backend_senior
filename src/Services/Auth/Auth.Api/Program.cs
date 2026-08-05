using BuildingBlocks;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Auth");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));

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

app.Run();

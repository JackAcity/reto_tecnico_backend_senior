using BuildingBlocks;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Control");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));

var app = builder.Build();
app.UseServiceDefaults("Control");

// Control es el único dueño del esquema (design.md §C11). Migra antes de atender
// tráfico: los demás servicios esperan por su health check, así que cuando responden
// 200 la base ya está lista.
await app.Services.MigrarAsync();

app.Run();

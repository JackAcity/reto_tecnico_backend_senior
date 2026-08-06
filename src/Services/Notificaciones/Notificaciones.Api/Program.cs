using BuildingBlocks;
using Mensajeria;
using Microsoft.Extensions.Options;
using Notificaciones.Api;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Notificaciones");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.AddMensajeria(builder.Configuration);
builder.Services.AddEnviadorCorreo(builder.Configuration);
builder.Services.AddScoped<ManejadorNotificacion>();
builder.Services.AddHostedService<ConsumidorNotificaciones>();

var app = builder.Build();
app.UseServiceDefaults("Notificaciones");

// Falla al arrancar y no en el primer correo: mismo criterio que el resto de
// la configuración de infraestructura (Jwt:Key, RabbitMq:*, SeaweedFs:Filer).
app.Services.GetRequiredService<IOptions<OpcionesSmtp>>().Value.Validar();

// El esquema ya existe: Notificaciones espera a que Control termine de migrar
// (depends_on en el compose, design.md §C11). No migra ni siembra nada.

app.Run();

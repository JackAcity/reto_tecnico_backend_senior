using ServiceHost;
using CargaMasiva.Api;
using CargaMasiva.Application;
using CargaMasiva.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("CargaMasiva");
builder.Services.AddPersistenciaCargaMasiva(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.AddAlmacenCargaSeaweedFs(builder.Configuration);
builder.Services.AddMensajeriaRabbit(builder.Configuration);

var cadenaPostgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres.");
builder.Services.AddScoped<IReglasCarga>(_ => new ReglasCargaSql(cadenaPostgres));
builder.Services.AddScoped<IInsertadorMasivo>(_ => new InsertadorMasivo(cadenaPostgres));
builder.Services.AddScoped<ILectorExcel, LectorExcel>();
builder.Services.AddScoped<IRepositorioCargas, RepositorioCargasEf>();
builder.Services.AddScoped<IPublicadorNotificacion, PublicadorNotificacionRabbit>();
builder.Services.AddScoped<ProcesadorLote>();
builder.Services.AddScoped<ManejadorCarga>();

builder.Services.AddHostedService<ConsumidorCargaMasiva>();

var app = builder.Build();
app.UseServiceDefaults("CargaMasiva");

// El esquema ya existe: CargaMasiva espera a que Control termine de migrar
// (depends_on en el compose, design.md §C11). No migra ni siembra nada.

app.Run();

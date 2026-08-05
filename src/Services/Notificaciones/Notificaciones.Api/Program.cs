using BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Notificaciones");

var app = builder.Build();
app.UseServiceDefaults("Notificaciones");

app.Run();

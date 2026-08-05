using BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("CargaMasiva");

var app = builder.Build();
app.UseServiceDefaults("CargaMasiva");

app.Run();

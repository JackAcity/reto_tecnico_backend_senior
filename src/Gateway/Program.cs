using BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Gateway");

var app = builder.Build();
app.UseServiceDefaults("Gateway");

app.Run();

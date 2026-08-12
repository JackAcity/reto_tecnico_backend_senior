using ServiceHost;
using Control.Api;
using Mensajeria;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Control");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.AddAlmacenCargasSeaweedFs(builder.Configuration);
builder.Services.AddMensajeria(builder.Configuration);
builder.Services.AddAutenticacionJwt(builder.Configuration);
builder.Services.AddScoped<IRepositorioCargas, RepositorioCargasEf>();
builder.Services.AddScoped<IConsultaCargas, ConsultaCargasEf>();
builder.Services.AddScoped<IPublicadorCargas, PublicadorCargasRabbit>();
builder.Services.AddScoped<ServicioCargas>();
builder.Services.AddScoped<ConsultaCargas>();

// Control mantiene el mismo techo de transporte que el gateway para tráfico interno.
var tamanoMaximoMb = builder.Configuration.GetValue("Carga:TamanoMaximoMb", 25);
var limiteTransporte = (tamanoMaximoMb + 1L) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteTransporte);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limiteTransporte);

var app = builder.Build();
app.UseServiceDefaults("Control");
app.UseAuthentication();
app.UseAuthorization();

// El servicio vuelve a exigir el permiso porque el usuario auditado proviene del token.
// La autenticación usa bearer tokens, no cookies.
app.MapPost("/cargas", async (
    [FromForm] IFormFile? archivo,
    ServicioCargas servicio,
    HttpContext http,
    CancellationToken ct) =>
{
    var error = ServicioCargas.ValidarArchivo(archivo?.FileName ?? "", archivo?.Length ?? 0, tamanoMaximoMb);
    if (error is not null)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["archivo"] = [error] });

    await using var contenido = archivo!.OpenReadStream();

    var errorFirma = await ServicioCargas.ValidarFirmaAsync(contenido, ct);
    if (errorFirma is not null)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["archivo"] = [errorFirma] });

    var usuario = http.User.FindFirst("email")?.Value ?? http.User.Identity?.Name ?? "desconocido";
    var correlationId = http.Response.Headers[ServiceDefaults.CorrelationHeader].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N");

    var resultado = await servicio.RegistrarAsync(contenido, archivo.FileName, archivo.Length, usuario, correlationId, ct);

    // El id queda disponible para auditoría aunque falle el encolado.
    return resultado.Error is null
        ? Results.Created($"/cargas/{resultado.IdCarga}", resultado)
        : Results.Json(resultado, statusCode: StatusCodes.Status502BadGateway);
})
.RequireAuthorization(Autenticacion.PoliticaCargaMasiva)
.DisableAntiforgery();

app.MapGet("/cargas", async (ConsultaCargas consulta, CancellationToken ct, int limite = 50) =>
        Results.Ok(await consulta.HistorialAsync(Math.Clamp(limite, 1, 200), ct)))
    .RequireAuthorization(Autenticacion.PoliticaAutenticado);

app.MapGet("/cargas/{id:int}", async (int id, ConsultaCargas consulta, CancellationToken ct, int limiteErrores = 100) =>
        await consulta.DetalleAsync(id, Math.Clamp(limiteErrores, 1, 1000), ct) is { } detalle
            ? Results.Ok(detalle)
            : Results.NotFound(new { title = "Carga no encontrada", idCarga = id }))
    .RequireAuthorization(Autenticacion.PoliticaAutenticado);

// El endpoint no expone la ruta interna del almacenamiento.
app.MapGet("/cargas/{id:int}/contenido", async (int id, ConsultaCargas consulta, IAlmacenCargas almacen, CancellationToken ct) =>
{
    var archivo = await consulta.ArchivoAsync(id, ct);
    if (archivo is null)
        return Results.NotFound(new { title = "Carga no encontrada o sin archivo asociado", idCarga = id });

    var contenido = await almacen.DescargarAsync(archivo.RutaArchivo, ct);
    return Results.File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", archivo.NombreArchivo);
})
    .RequireAuthorization(Autenticacion.PoliticaAutenticado);

app.Run();

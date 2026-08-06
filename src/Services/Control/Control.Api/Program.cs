using Almacenamiento;
using BuildingBlocks;
using Control.Api;
using Mensajeria;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Control");
builder.Services.AddPersistencia(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.AddAlmacenamiento(builder.Configuration);
builder.Services.AddMensajeria(builder.Configuration);
builder.Services.AddAutenticacionJwt(builder.Configuration);
builder.Services.AddScoped<ServicioCargas>();   // comando: §2️⃣, escritura
builder.Services.AddScoped<ConsultaCargas>();   // consulta: §5️⃣, solo lectura

// §C12 — los mismos tres techos que el gateway, porque Control también puede
// recibir tráfico directo dentro de la red de contenedores.
var tamanoMaximoMb = builder.Configuration.GetValue("Carga:TamanoMaximoMb", 25);
var limiteTransporte = (tamanoMaximoMb + 1L) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteTransporte);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limiteTransporte);

var app = builder.Build();
app.UseServiceDefaults("Control");
app.UseAuthentication();
app.UseAuthorization();

// Control es el único dueño del esquema (design.md §C11). Migra antes de atender
// tráfico: los demás servicios esperan por su health check, así que cuando responden
// 200 la base ya está lista.
await app.Services.MigrarAsync();

// §2️⃣ — subida. El permiso ya lo exigió el gateway; se vuelve a exigir acá porque
// el usuario auditado sale del token y no de una cabecera que cualquiera podría poner.
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

    // La extensión la controla el cliente; esto valida que el contenido en sí
    // empiece como zip, para no gastar SeaweedFS + una cola + un ciclo de
    // CargaMasiva en un archivo que iba a fallar de todos modos.
    var errorFirma = await ServicioCargas.ValidarFirmaAsync(contenido, ct);
    if (errorFirma is not null)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["archivo"] = [errorFirma] });

    var usuario = http.User.FindFirst("email")?.Value ?? http.User.Identity?.Name ?? "desconocido";
    var correlationId = http.Response.Headers[ServiceDefaults.CorrelationHeader].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N");

    var resultado = await servicio.RegistrarAsync(contenido, archivo.FileName, archivo.Length, usuario, correlationId, ct);

    // La carga quedó registrada y auditada aunque el encolado falle (§C7): por eso
    // el 502 también devuelve el idCarga, que ya es consultable en el historial.
    return resultado.Error is null
        ? Results.Created($"/cargas/{resultado.IdCarga}", resultado)
        : Results.Json(resultado, statusCode: StatusCodes.Status502BadGateway);
})
.RequireAuthorization(Autenticacion.PoliticaCargaMasiva)
.DisableAntiforgery();   // API con Bearer, sin cookies: el token antiforgery no aplica

// §5️⃣ — historial para el cliente web y para el polling de estados.
app.MapGet("/cargas", async (ConsultaCargas consulta, CancellationToken ct, int limite = 50) =>
        Results.Ok(await consulta.HistorialAsync(Math.Clamp(limite, 1, 200), ct)))
    .RequireAuthorization(Autenticacion.PoliticaAutenticado);

// §3.3c — detalle con los periodos resueltos y los fallidos auditados.
app.MapGet("/cargas/{id:int}", async (int id, ConsultaCargas consulta, CancellationToken ct, int limiteErrores = 100) =>
        await consulta.DetalleAsync(id, Math.Clamp(limiteErrores, 1, 1000), ct) is { } detalle
            ? Results.Ok(detalle)
            : Results.NotFound(new { title = "Carga no encontrada", idCarga = id }))
    .RequireAuthorization(Autenticacion.PoliticaAutenticado);

app.Run();

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Control.Api;

/// <summary>Adaptador SeaweedFS del puerto que posee Control.</summary>
public sealed class AlmacenCargasSeaweedFs(HttpClient http) : IAlmacenCargas
{
    private const string Esquema = "seaweed://";
    private const string Carpeta = "cargas";

    public async Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct)
    {
        var nombreSeguro = Uri.EscapeDataString(Path.GetFileName(nombreArchivo));
        var rutaRelativa = $"{Carpeta}/{Guid.NewGuid():N}/{nombreSeguro}";

        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, ct);
        using var formulario = new MultipartFormDataContent
        {
            { new ByteArrayContent(memoria.ToArray()), "file", nombreArchivo }
        };

        using var respuesta = await http.PostAsync(rutaRelativa, formulario, ct);
        respuesta.EnsureSuccessStatusCode();
        return Esquema + rutaRelativa;
    }
    public async Task<Stream> DescargarAsync(string ruta, CancellationToken ct = default)
    {
        var rutaRelativa = ruta.StartsWith(Esquema, StringComparison.Ordinal)
            ? ruta[Esquema.Length..]
            : throw new ArgumentException($"Ruta de almacenamiento inválida: '{ruta}'.", nameof(ruta));

        using var respuesta = await http.GetAsync(rutaRelativa, HttpCompletionOption.ResponseHeadersRead, ct);
        respuesta.EnsureSuccessStatusCode();

        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, ct);
        memoria.Position = 0;
        return memoria;
    }
}

public static class AlmacenCargasSeaweedFsExtensiones
{
    public static IServiceCollection AddAlmacenCargasSeaweedFs(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var filer = configuracion["SeaweedFs:Filer"]
            ?? throw new InvalidOperationException("Falta SeaweedFs:Filer.");

        servicios.AddHttpClient<IAlmacenCargas, AlmacenCargasSeaweedFs>(cliente =>
        {
            cliente.BaseAddress = new Uri(filer.EndsWith('/') ? filer : filer + "/");
            cliente.Timeout = TimeSpan.FromMinutes(9);
        })
        .AddStandardResilienceHandler(opciones =>
        {
            opciones.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
            opciones.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(8);
        });

        return servicios;
    }
}
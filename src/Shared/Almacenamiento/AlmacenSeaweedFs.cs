using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Almacenamiento;

/// <summary>
/// Puerto de almacenamiento. Control sube y CargaMasiva descarga; ninguno de los
/// dos conoce SeaweedFS más allá de esta interfaz.
/// </summary>
public interface IAlmacenArchivos
{
    /// <summary>Sube el archivo y devuelve la ruta en el formato literal del enunciado: <c>seaweed://...</c>.</summary>
    Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default);

    /// <summary>Descarga por la ruta devuelta por <see cref="SubirAsync"/>.</summary>
    Task<Stream> DescargarAsync(string ruta, CancellationToken ct = default);
}

/// <summary>
/// Cliente del filer HTTP de SeaweedFS. Es una API REST plana: PUT/GET sobre la
/// ruta del archivo, sin SDK de por medio.
/// </summary>
public sealed class AlmacenSeaweedFs(HttpClient http) : IAlmacenArchivos
{
    public const string Esquema = "seaweed://";
    public const string Carpeta = "cargas";

    public async Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        // Un prefijo único por subida evita que dos archivos con el mismo nombre
        // se pisen, sin tener que consultar antes si la ruta ya existe.
        var rutaRelativa = $"{Carpeta}/{Guid.NewGuid():N}/{Path.GetFileName(nombreArchivo)}";

        // El cuerpo va como bytes y no como StreamContent: si el handler de
        // resiliencia reintenta, un stream ya consumido enviaría un archivo vacío.
        // El tamaño está acotado por la validación de negocio (25 MB por defecto).
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, ct);

        using var formulario = new MultipartFormDataContent { { new ByteArrayContent(memoria.ToArray()), "file", nombreArchivo } };
        var respuesta = await http.PostAsync(rutaRelativa, formulario, ct);
        respuesta.EnsureSuccessStatusCode();

        return Esquema + rutaRelativa;
    }

    public async Task<Stream> DescargarAsync(string ruta, CancellationToken ct = default)
    {
        var respuesta = await http.GetAsync(RutaRelativa(ruta), HttpCompletionOption.ResponseHeadersRead, ct);
        respuesta.EnsureSuccessStatusCode();

        return await respuesta.Content.ReadAsStreamAsync(ct);
    }

    private static string RutaRelativa(string ruta) =>
        ruta.StartsWith(Esquema, StringComparison.Ordinal)
            ? ruta[Esquema.Length..]
            : throw new ArgumentException($"Ruta de almacenamiento inválida: '{ruta}'.", nameof(ruta));
}

public static class AlmacenamientoExtensiones
{
    /// <summary>
    /// El filer puede reiniciar o estar arrancando: el handler estándar aporta
    /// reintentos con jitter, timeout y circuit breaker en una línea (valorado
    /// por el enunciado, cero código propio de resiliencia).
    /// </summary>
    public static IServiceCollection AddAlmacenamiento(this IServiceCollection servicios, IConfiguration config)
    {
        var filer = config["SeaweedFs:Filer"] ?? "http://seaweedfs:8888";

        servicios.AddHttpClient<IAlmacenArchivos, AlmacenSeaweedFs>(c =>
        {
            c.BaseAddress = new Uri(filer.EndsWith('/') ? filer : filer + "/");
            c.Timeout = TimeSpan.FromMinutes(5);   // subidas de decenas de MB
        })
        .AddStandardResilienceHandler(o =>
        {
            // Los valores por omisión (30 s totales, 10 s por intento) están pensados
            // para APIs JSON, no para subir 25 MB.
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            o.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);   // exigido: ≥ 2× el timeout por intento
        });

        return servicios;
    }
}

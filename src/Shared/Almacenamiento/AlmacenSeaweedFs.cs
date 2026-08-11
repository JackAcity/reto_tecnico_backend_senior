using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Almacenamiento;

/// <summary>Puerto de almacenamiento de archivos para los casos de uso.</summary>
public interface IAlmacenArchivos
{
    /// <summary>Sube el archivo y devuelve la ruta en el formato literal del enunciado: <c>seaweed://...</c>.</summary>
    Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default);

    /// <summary>Descarga por la ruta devuelta por <see cref="SubirAsync"/>.</summary>
    Task<Stream> DescargarAsync(string ruta, CancellationToken ct = default);
}

/// <summary>Adaptador HTTP del filer de SeaweedFS.</summary>
public sealed class AlmacenSeaweedFs(HttpClient http) : IAlmacenArchivos
{
    public const string Esquema = "seaweed://";
    public const string Carpeta = "cargas";

    public async Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        // Una ruta única y escapada evita colisiones e inyección de segmentos de ruta.
        var nombreSeguro = Uri.EscapeDataString(Path.GetFileName(nombreArchivo));
        var rutaRelativa = $"{Carpeta}/{Guid.NewGuid():N}/{nombreSeguro}";

        // El reintento necesita un cuerpo reutilizable; un stream ya consumido no lo es.
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

        // ExcelDataReader necesita Seek y el stream HTTP no lo ofrece.
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, ct);
        memoria.Position = 0;
        return memoria;
    }

    private static string RutaRelativa(string ruta) =>
        ruta.StartsWith(Esquema, StringComparison.Ordinal)
            ? ruta[Esquema.Length..]
            : throw new ArgumentException($"Ruta de almacenamiento inválida: '{ruta}'.", nameof(ruta));
}

public static class AlmacenamientoExtensiones
{
    /// <summary>Registra el cliente con la política estándar de resiliencia.</summary>
    public static IServiceCollection AddAlmacenamiento(this IServiceCollection servicios, IConfiguration config)
    {
        // Una configuración ausente debe fallar al arrancar, no apuntar a un host implícito.
        var filer = config["SeaweedFs:Filer"]
            ?? throw new InvalidOperationException("Falta SeaweedFs:Filer.");

        servicios.AddHttpClient<IAlmacenArchivos, AlmacenSeaweedFs>(c =>
        {
            c.BaseAddress = new Uri(filer.EndsWith('/') ? filer : filer + "/");
            c.Timeout = TimeSpan.FromMinutes(9);
        })
        .AddStandardResilienceHandler(o =>
        {
            // Los valores predeterminados sirven para JSON, no para cargas de archivos.
            o.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);

            // Debe cubrir todos los intentos y su backoff; un total menor los cancelaría antes.
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(8);
        });

        return servicios;
    }
}

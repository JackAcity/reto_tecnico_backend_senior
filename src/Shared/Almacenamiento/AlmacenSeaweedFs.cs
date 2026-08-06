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
        // Path.GetFileName (semántica Linux del contenedor: solo "/" separa) ya
        // descarta cualquier componente de directorio — nada de "../" sobrevive.
        // Uri.EscapeDataString es la segunda mitad: sin ella, un nombre con tilde,
        // espacio o "#" (frecuente en español: "Catálogo Q1.xlsx") rompe la URI
        // que se arma para el filer, o peor, la trunca en un punto inesperado.
        var nombreSeguro = Uri.EscapeDataString(Path.GetFileName(nombreArchivo));
        var rutaRelativa = $"{Carpeta}/{Guid.NewGuid():N}/{nombreSeguro}";

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

        // ExcelDataReader necesita Seek: un .xlsx es un zip por dentro, y para leer
        // sus entradas hay que ir primero al directorio central al final del
        // archivo — no es de verdad forward-only pese a lo que dice el comentario
        // de LectorExcel. El stream de red no soporta Seek (HttpBaseStream lo
        // lanza), así que se buffer­iza en memoria antes de devolverlo. El tamaño
        // ya está acotado por la validación de negocio (25 MB, §C12).
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
    /// <summary>
    /// El filer puede reiniciar o estar arrancando: el handler estándar aporta
    /// reintentos con jitter, timeout y circuit breaker en una línea (valorado
    /// por el enunciado, cero código propio de resiliencia).
    /// </summary>
    public static IServiceCollection AddAlmacenamiento(this IServiceCollection servicios, IConfiguration config)
    {
        // Sin fallback: "http://seaweedfs:8888" siempre funcionaba en docker-compose
        // (.env/el propio compose lo inyecta) y por eso mismo escondía el error si
        // algún día un servicio corre suelto sin configurar nada — apuntaría en
        // silencio a un host que puede no existir fuera de la red de contenedores.
        var filer = config["SeaweedFs:Filer"]
            ?? throw new InvalidOperationException("Falta SeaweedFs:Filer.");

        servicios.AddHttpClient<IAlmacenArchivos, AlmacenSeaweedFs>(c =>
        {
            c.BaseAddress = new Uri(filer.EndsWith('/') ? filer : filer + "/");
            c.Timeout = TimeSpan.FromMinutes(9);   // por encima del TotalRequestTimeout de abajo
        })
        .AddStandardResilienceHandler(o =>
        {
            // Los valores por omisión (30 s totales, 10 s por intento) están pensados
            // para APIs JSON, no para subir 25 MB.
            o.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);   // exigido: ≥ 2× el timeout por intento

            // MaxRetryAttempts por omisión es 3 (no se toca acá): en el peor caso,
            // 3 intentos × 2 min + el backoff exponencial entre ellos pueden sumar
            // más de 6 min. Un TotalRequestTimeout de 5 min (el valor anterior)
            // cortaba el tercer intento a mitad de camino, reduciendo el retry real
            // por debajo de los 3 que este código dice que configura — el mismo
            // tipo de número que "cierra" sin serlo. 8 min cubre el peor caso con margen.
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(8);
        });

        return servicios;
    }
}

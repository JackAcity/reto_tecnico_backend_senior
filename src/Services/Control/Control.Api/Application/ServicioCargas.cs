using BuildingBlocks;
using CargaMasiva.Domain;
using Microsoft.Extensions.Logging;

namespace Control.Api;

/// <summary>
/// Puerto de acceso a datos de <see cref="ServicioCargas"/> (design.md §D2 de
/// arquitectura-hexagonal-transversal): Application no conoce EF Core.
/// </summary>
public interface IRepositorioCargas
{
    void Agregar(CargaArchivo carga);
    Task GuardarCambiosAsync(CancellationToken ct);
}

/// <summary>Puerto de almacenamiento que necesita el caso de uso de registro.</summary>
public interface IAlmacenCargas
{
    Task<string> SubirAsync(Stream contenido, string nombreArchivo, CancellationToken ct);
}

/// <summary>Publica el comando de procesamiento de una carga recién registrada.</summary>
public interface IPublicadorCargas
{
    Task<Resultado> PublicarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct);
}

public sealed record ResultadoRegistro(int IdCarga, string Estado, string? Error = null);

/// <summary>
/// El comando del §2️⃣: validar, guardar el archivo, registrar la carga y publicar.
/// Solo escritura — las consultas viven en <see cref="ConsultaCargas"/> (CQRS-lite,
/// design.md §3). Fuera de los endpoints para que el orden de los pasos —que es lo
/// que el §C7 pone en juego— se pueda probar sin HTTP.
/// </summary>
public sealed class ServicioCargas(
    IRepositorioCargas repositorio,
    IAlmacenCargas almacen,
    IPublicadorCargas publicador,
    ILogger<ServicioCargas> log)
{
    public const string ExtensionPermitida = ".xlsx";

    public static string? ValidarArchivo(string nombreArchivo, long tamanoBytes, int tamanoMaximoMb)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
            return "El archivo es obligatorio.";

        if (!Path.GetExtension(nombreArchivo).Equals(ExtensionPermitida, StringComparison.OrdinalIgnoreCase))
            return $"Solo se aceptan archivos {ExtensionPermitida}.";

        if (tamanoBytes <= 0)
            return "El archivo está vacío.";

        // La validación de negocio corre ANTES del techo de transporte, para que el
        // usuario reciba este mensaje y no un 413 crudo del gateway (§C12).
        var maximoBytes = tamanoMaximoMb * 1024L * 1024L;
        if (tamanoBytes > maximoBytes)
            return $"El archivo supera el máximo de {tamanoMaximoMb} MB.";

        return null;
    }

    /// <summary>Firma ZIP local-file-header: todo <c>.xlsx</c> (OOXML) es, por dentro, un zip.</summary>
    private static readonly byte[] FirmaZip = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// La extensión es un metadato que el cliente controla — renombrar cualquier
    /// archivo a <c>.xlsx</c> pasa <see cref="ValidarArchivo"/> sin problema. Esto
    /// verifica que el contenido empiece como zip antes de aceptarlo, para no
    /// gastar SeaweedFS + una cola + un ciclo de CargaMasiva en basura que iba a
    /// fallar de todos modos al intentar leerse como Excel.
    /// </summary>
    public static async Task<string?> ValidarFirmaAsync(Stream contenido, CancellationToken ct = default)
    {
        var firma = new byte[FirmaZip.Length];
        var leidos = await contenido.ReadAsync(firma, ct);
        contenido.Position = 0;   // el stream se reutiliza para la subida real

        return leidos == FirmaZip.Length && firma.AsSpan().SequenceEqual(FirmaZip)
            ? null
            : "El archivo no es un .xlsx válido (no tiene firma binaria de un archivo ZIP/OOXML).";
    }

    public async Task<ResultadoRegistro> RegistrarAsync(
        Stream contenido, string nombreArchivo, long tamanoBytes, string usuario, string correlationId, CancellationToken ct = default)
    {
        // 1. El archivo primero: si el almacenamiento falla no queda una carga
        //    huérfana apuntando a una ruta que no existe.
        var ruta = await almacen.SubirAsync(contenido, nombreArchivo, ct);

        // 2. Auditoría de quién y cuándo (§2️⃣), con el estado inicial del enunciado.
        var carga = new CargaArchivo
        {
            NombreArchivo = nombreArchivo,
            RutaArchivo = ruta,
            TamanoBytes = tamanoBytes,
            Usuario = usuario,
            FechaRegistro = DateTimeOffset.UtcNow,
            Estado = EstadoCarga.Pendiente,
            CorrelationId = correlationId
        };
        repositorio.Agregar(carga);
        await repositorio.GuardarCambiosAsync(ct);

        // 3. Publicar es un dual write (§C7): no hay transacción común entre la base
        //    y el broker. Se publica inmediatamente después del commit y, si falla,
        //    la carga queda en el estado terminal Fallida en vez de quedar colgada
        //    en Pendiente para siempre. El patrón correcto sería Transactional
        //    Outbox; está declarado como fuera de alcance en el README.
        // Resultado, no catch (design.md §D4): un fallo esperado de publicación se
        // comunica como Resultado.Fallo; un bug real dentro de IPublicador se
        // propaga como excepción no controlada, no se confunde con esto.
        var resultado = await publicador.PublicarAsync(
            new MensajeCarga(carga.Id, ruta, usuario), correlationId, ct);

        if (!resultado.EsExitoso)
        {
            log.LogError("No se pudo publicar la carga {IdCarga}; queda como Fallida: {Error}", carga.Id, resultado.Error);

            carga.Transicionar(EstadoCarga.Fallida);
            carga.MensajeError = $"No se pudo encolar el procesamiento: {resultado.Error}";
            await repositorio.GuardarCambiosAsync(ct);

            return new ResultadoRegistro(carga.Id, carga.Estado.ToString(), carga.MensajeError);
        }

        return new ResultadoRegistro(carga.Id, carga.Estado.ToString());
    }
}

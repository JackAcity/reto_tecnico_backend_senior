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
    Task<Stream> DescargarAsync(string ruta, CancellationToken ct = default);
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

        // El límite de negocio devuelve un error útil antes de que el gateway responda 413.
        var maximoBytes = tamanoMaximoMb * 1024L * 1024L;
        if (tamanoBytes > maximoBytes)
            return $"El archivo supera el máximo de {tamanoMaximoMb} MB.";

        return null;
    }

    /// <summary>Cabecera ZIP que identifica un contenedor OOXML.</summary>
    private static readonly byte[] FirmaZip = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>Evita encolar archivos renombrados con extensión <c>.xlsx</c>.</summary>
    public static async Task<string?> ValidarFirmaAsync(Stream contenido, CancellationToken ct = default)
    {
        var firma = new byte[FirmaZip.Length];
        var leidos = await contenido.ReadAsync(firma, ct);
        contenido.Position = 0;

        return leidos == FirmaZip.Length && firma.AsSpan().SequenceEqual(FirmaZip)
            ? null
            : "El archivo no es un .xlsx válido (no tiene firma binaria de un archivo ZIP/OOXML).";
    }

    public async Task<ResultadoRegistro> RegistrarAsync(
        Stream contenido, string nombreArchivo, long tamanoBytes, string usuario, string correlationId, CancellationToken ct = default)
    {
        // Persistir el archivo primero evita una carga que apunte a una ruta inexistente.
        var ruta = await almacen.SubirAsync(contenido, nombreArchivo, ct);

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

        // Base y broker no comparten transacción. Si publicar falla de forma esperada,
        // la carga se cierra como Fallida en lugar de quedar Pendiente indefinidamente.
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

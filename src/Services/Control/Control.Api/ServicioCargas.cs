using Almacenamiento;
using CargaMasiva.Domain;
using Mensajeria;
using Persistencia;

namespace Control.Api;

public sealed record ResultadoRegistro(int IdCarga, string Estado, string? Error = null);

/// <summary>
/// El comando del §2️⃣: validar, guardar el archivo, registrar la carga y publicar.
/// Solo escritura — las consultas viven en <see cref="ConsultaCargas"/> (CQRS-lite,
/// design.md §3). Fuera de los endpoints para que el orden de los pasos —que es lo
/// que el §C7 pone en juego— se pueda probar sin HTTP.
/// </summary>
public sealed class ServicioCargas(
    RetoDbContext db,
    IAlmacenArchivos almacen,
    IPublicador publicador,
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
        db.CargaArchivos.Add(carga);
        await db.SaveChangesAsync(ct);

        // 3. Publicar es un dual write (§C7): no hay transacción común entre la base
        //    y el broker. Se publica inmediatamente después del commit y, si falla,
        //    la carga queda en el estado terminal Fallida en vez de quedar colgada
        //    en Pendiente para siempre. El patrón correcto sería Transactional
        //    Outbox; está declarado como fuera de alcance en el README.
        try
        {
            await publicador.PublicarAsync(
                Topologia.RkCarga, new MensajeCarga(carga.Id, ruta, usuario), correlationId, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "No se pudo publicar la carga {IdCarga}; queda como Fallida", carga.Id);

            carga.Transicionar(EstadoCarga.Fallida);
            carga.MensajeError = $"No se pudo encolar el procesamiento: {ex.Message}";
            await db.SaveChangesAsync(ct);

            return new ResultadoRegistro(carga.Id, carga.Estado.ToString(), carga.MensajeError);
        }

        return new ResultadoRegistro(carga.Id, carga.Estado.ToString());
    }
}

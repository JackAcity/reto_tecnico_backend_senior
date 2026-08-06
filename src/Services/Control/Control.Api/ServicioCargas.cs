using Almacenamiento;
using CargaMasiva.Domain;
using Mensajeria;
using Microsoft.EntityFrameworkCore;
using Persistencia;

namespace Control.Api;

public sealed record ResultadoRegistro(int IdCarga, string Estado, string? Error = null);

/// <summary>
/// El caso de uso del §2️⃣: validar, guardar el archivo, registrar la carga y
/// publicar. Fuera de los endpoints para que el orden de los pasos —que es lo
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

    public async Task<IReadOnlyList<ResumenCarga>> HistorialAsync(int limite, CancellationToken ct = default) =>
        await db.CargaArchivos
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Take(limite)
            .Select(c => new ResumenCarga(
                c.Id, c.NombreArchivo, c.Usuario, c.FechaRegistro, c.Estado.ToString(),
                c.TotalFilas, c.FilasInsertadas, c.FilasRechazadas, c.FechaFin))
            .ToListAsync(ct);

    public async Task<DetalleCarga?> DetalleAsync(int idCarga, int limiteErrores, CancellationToken ct = default)
    {
        var carga = await db.CargaArchivos
            .AsNoTracking()
            .Include(c => c.Periodos)
            .SingleOrDefaultAsync(c => c.Id == idCarga, ct);

        if (carga is null)
            return null;

        // El detalle de errores puede ser grande: se acota y se informa el total.
        var totalErrores = await db.DetalleCargaErrores.CountAsync(e => e.CargaArchivoId == idCarga, ct);
        var errores = await db.DetalleCargaErrores
            .AsNoTracking()
            .Where(e => e.CargaArchivoId == idCarga)
            .OrderBy(e => e.Id)
            .Take(limiteErrores)
            .Select(e => new ErrorAuditado(
                e.NumeroFila, e.Periodo, e.CodigoProducto, e.Columna, e.Motivo.ToString(), e.ValorCrudo))
            .ToListAsync(ct);

        return new DetalleCarga(
            new ResumenCarga(carga.Id, carga.NombreArchivo, carga.Usuario, carga.FechaRegistro,
                carga.Estado.ToString(), carga.TotalFilas, carga.FilasInsertadas, carga.FilasRechazadas, carga.FechaFin),
            carga.RutaArchivo,
            carga.MensajeError,
            carga.CorrelationId,
            [.. carga.Periodos.OrderBy(p => p.Periodo).Select(p => new PeriodoCarga(p.Periodo, p.Estado, p.FilasInsertadas))],
            errores,
            totalErrores);
    }
}

public sealed record ResumenCarga(
    int IdCarga, string NombreArchivo, string Usuario, DateTimeOffset FechaRegistro, string Estado,
    int TotalFilas, int FilasInsertadas, int FilasRechazadas, DateTimeOffset? FechaFin);

public sealed record PeriodoCarga(string Periodo, string Estado, int FilasInsertadas);

public sealed record ErrorAuditado(
    int NumeroFila, string? Periodo, string? CodigoProducto, string? Columna, string Motivo, string? ValorCrudo);

public sealed record DetalleCarga(
    ResumenCarga Carga, string? RutaArchivo, string? MensajeError, string CorrelationId,
    IReadOnlyList<PeriodoCarga> Periodos, IReadOnlyList<ErrorAuditado> Errores, int TotalErrores);

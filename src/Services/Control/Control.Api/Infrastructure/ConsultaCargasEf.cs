using Microsoft.EntityFrameworkCore;

namespace Control.Api;

/// <summary>Adaptador EF de <see cref="IConsultaCargas"/> (design.md §D2) — todas las consultas <c>AsNoTracking</c> sin excepción.</summary>
public sealed class ConsultaCargasEf(ControlDbContext db) : IConsultaCargas
{
    public async Task<IReadOnlyList<ResumenCarga>> HistorialAsync(int limite, CancellationToken ct) =>
        await db.CargaArchivos
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Take(limite)
            .Select(c => new ResumenCarga(
                c.Id, c.NombreArchivo, c.Usuario, c.FechaRegistro, c.Estado.ToString(),
                c.TotalFilas, c.FilasInsertadas, c.FilasRechazadas, c.FechaFin))
            .ToListAsync(ct);

    public async Task<DetalleCarga?> DetalleAsync(int idCarga, int limiteErrores, CancellationToken ct)
    {
        var carga = await db.CargaArchivos
            .AsNoTracking()
            .Include(c => c.Periodos)
            .SingleOrDefaultAsync(c => c.Id == idCarga, ct);

        if (carga is null)
            return null;

        // El payload se acota con Take, pero el contrato expone totalErrores exacto
        // y CountAsync debe recorrer todas las coincidencias. La prueba real de una
        // carga con 2M errores superó 60 s incluso con limiteErrores=1; el índice por
        // carga_archivo_id evita escanear otras cargas, pero no elimina el coste de
        // contar 2M filas propias. El polling debe usar HistorialAsync. Cambiar a un
        // contador mantenido al escribir o a paginación sin total exacto alteraría el
        // contrato y exige una decisión explícita, no una optimización oculta aquí.
        var totalErrores = await db.DetalleCargaErrores.CountAsync(e => e.CargaArchivoId == idCarga, ct);
        var errores = await db.DetalleCargaErrores
            .AsNoTracking()
            .Where(e => e.CargaArchivoId == idCarga)
            .OrderBy(e => e.Id)
            .Take(limiteErrores)
            .Select(e => new ErrorAuditado(
                e.NumeroFila, e.Periodo, e.CodigoProducto, e.Columna, e.Motivo, e.ValorCrudo))
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

    public async Task<ArchivoDeCarga?> ArchivoAsync(int idCarga, CancellationToken ct) =>
        await db.CargaArchivos
            .AsNoTracking()
            .Where(c => c.Id == idCarga && c.RutaArchivo != null)
            .Select(c => new ArchivoDeCarga(c.NombreArchivo, c.RutaArchivo!))
            .SingleOrDefaultAsync(ct);
}

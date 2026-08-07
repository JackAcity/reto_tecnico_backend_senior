using Microsoft.EntityFrameworkCore;
using Persistencia;

namespace Control.Api;

public sealed record ResumenCarga(
    int IdCarga, string NombreArchivo, string Usuario, DateTimeOffset FechaRegistro, string Estado,
    int TotalFilas, int FilasInsertadas, int FilasRechazadas, DateTimeOffset? FechaFin);

public sealed record PeriodoCarga(string Periodo, string Estado, int FilasInsertadas);

public sealed record ErrorAuditado(
    int NumeroFila, string? Periodo, string? CodigoProducto, string? Columna, string Motivo, string? ValorCrudo);

public sealed record DetalleCarga(
    ResumenCarga Carga, string? RutaArchivo, string? MensajeError, string CorrelationId,
    IReadOnlyList<PeriodoCarga> Periodos, IReadOnlyList<ErrorAuditado> Errores, int TotalErrores);

public sealed record ArchivoDeCarga(string NombreArchivo, string RutaArchivo);

/// <summary>
/// El lado de lectura del §5️⃣: historial y detalle. Separado de <see cref="ServicioCargas"/>
/// (CQRS-lite, design.md §3) — nunca escribe, así que no necesita <c>IAlmacenArchivos</c>
/// ni <c>IPublicador</c>, y sus consultas se marcan <c>AsNoTracking</c> sin excepción.
/// </summary>
public sealed class ConsultaCargas(RetoDbContext db)
{
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

    /// <summary>§2.1e — "consultar el contenido del archivo excel subido": solo lo mínimo para poder descargarlo desde SeaweedFS.</summary>
    public async Task<ArchivoDeCarga?> ArchivoAsync(int idCarga, CancellationToken ct = default) =>
        await db.CargaArchivos
            .AsNoTracking()
            .Where(c => c.Id == idCarga && c.RutaArchivo != null)
            .Select(c => new ArchivoDeCarga(c.NombreArchivo, c.RutaArchivo!))
            .SingleOrDefaultAsync(ct);
}

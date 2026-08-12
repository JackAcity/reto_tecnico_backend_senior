using CargaMasiva.Application;
using CargaMasiva.Domain;
using Microsoft.EntityFrameworkCore;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// Adaptador EF de <see cref="IRepositorioCargas"/> (design.md §D1). Único tipo
/// de <c>CargaMasiva.Infrastructure</c> que conoce <see cref="CargaMasivaDbContext"/>
/// para este puerto — mismo <see cref="CargaMasivaDbContext"/> con tracking activo
/// durante todo el scope del mensaje, así que <c>CargaArchivo.Transicionar</c>
/// (llamado directo sobre la entidad en <c>ManejadorCarga</c>) se persiste en
/// el siguiente <see cref="GuardarCambiosAsync"/> sin necesitar un método
/// explícito de "guardar entidad".
/// </summary>
public sealed class RepositorioCargasEf(CargaMasivaDbContext db) : IRepositorioCargas
{
    public Task<CargaArchivo> ObtenerAsync(int idCarga, CancellationToken ct) =>
        db.CargaArchivos.SingleAsync(c => c.Id == idCarga, ct);

    public async Task<IReadOnlyList<CargaPeriodo>> ObtenerPeriodosAsync(int idCarga, CancellationToken ct) =>
        await db.CargaPeriodos.Where(p => p.CargaArchivoId == idCarga).ToListAsync(ct);

    public void AgregarErrores(IEnumerable<DetalleCargaError> errores) =>
        db.DetalleCargaErrores.AddRange(errores);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

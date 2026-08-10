using Microsoft.EntityFrameworkCore;
using Persistencia;

namespace Notificaciones.Api;

/// <summary>Adaptador EF de <see cref="IRepositorioNotificaciones"/> (design.md §D2).</summary>
public sealed class RepositorioNotificacionesEf(RetoDbContext db) : IRepositorioNotificaciones
{
    public Task<CargaArchivo> ObtenerAsync(int idCarga, CancellationToken ct) =>
        db.CargaArchivos.SingleAsync(c => c.Id == idCarga, ct);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

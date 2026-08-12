using Microsoft.EntityFrameworkCore;

namespace Notificaciones.Api;

/// <summary>Adaptador EF de <see cref="IRepositorioNotificaciones"/> (design.md §D2).</summary>
public sealed class RepositorioNotificacionesEf(NotificacionesDbContext db) : IRepositorioNotificaciones
{
    public Task<CargaPorNotificar> ObtenerAsync(int idCarga, CancellationToken ct) =>
        db.CargaArchivos.SingleAsync(c => c.Id == idCarga, ct);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

using CargaMasiva.Domain;
using Persistencia;

namespace Control.Api;

/// <summary>Adaptador EF de <see cref="IRepositorioCargas"/> (design.md §D2).</summary>
public sealed class RepositorioCargasEf(RetoDbContext db) : IRepositorioCargas
{
    public void Agregar(CargaArchivo carga) => db.CargaArchivos.Add(carga);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

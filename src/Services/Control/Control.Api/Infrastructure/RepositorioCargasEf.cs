
namespace Control.Api;

/// <summary>Adaptador EF de <see cref="IRepositorioCargas"/> (design.md §D2).</summary>
public sealed class RepositorioCargasEf(ControlDbContext db) : IRepositorioCargas
{
    public void Agregar(RegistroCarga carga) => db.CargaArchivos.Add(carga);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

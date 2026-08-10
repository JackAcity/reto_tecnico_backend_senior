using CargaMasiva.Domain;

namespace CargaMasiva.Application;

/// <summary>
/// Puerto de acceso a datos de <see cref="ManejadorCarga"/> (design.md §D1 de
/// arquitectura-hexagonal-transversal): Application no conoce EF Core. Angosto
/// a propósito (ISP) — solo las operaciones que el caso de uso efectivamente
/// usa, no un repositorio genérico de <c>CargaArchivo</c>.
/// </summary>
public interface IRepositorioCargas
{
    Task<CargaArchivo> ObtenerAsync(int idCarga, CancellationToken ct);
    Task<IReadOnlyList<CargaPeriodo>> ObtenerPeriodosAsync(int idCarga, CancellationToken ct);
    void AgregarErrores(IEnumerable<DetalleCargaError> errores);
    Task GuardarCambiosAsync(CancellationToken ct);
}

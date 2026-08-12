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
/// Puerto de lectura de <see cref="ConsultaCargas"/> (design.md §D2 de
/// arquitectura-hexagonal-transversal): Application no conoce EF Core.
/// </summary>
public interface IConsultaCargas
{
    Task<IReadOnlyList<ResumenCarga>> HistorialAsync(int limite, CancellationToken ct);
    Task<DetalleCarga?> DetalleAsync(int idCarga, int limiteErrores, CancellationToken ct);
    Task<ArchivoDeCarga?> ArchivoAsync(int idCarga, CancellationToken ct);
}

/// <summary>
/// El lado de lectura del §5️⃣: historial y detalle. Separado de <see cref="ServicioCargas"/>
/// (CQRS-lite, design.md §3) — nunca escribe, así que no necesita <c>IAlmacenCargas</c>
/// ni <c>IPublicador</c>. Delega en <see cref="IConsultaCargas"/> (Infrastructure): el
/// mismo nombre que el resto de casos de uso inyecta desde Program.cs, sin importar
/// dónde vive la query real.
/// </summary>
public sealed class ConsultaCargas(IConsultaCargas consulta)
{
    public Task<IReadOnlyList<ResumenCarga>> HistorialAsync(int limite, CancellationToken ct = default) =>
        consulta.HistorialAsync(limite, ct);

    public Task<DetalleCarga?> DetalleAsync(int idCarga, int limiteErrores, CancellationToken ct = default) =>
        consulta.DetalleAsync(idCarga, limiteErrores, ct);

    /// <summary>§2.1e — "consultar el contenido del archivo excel subido": solo lo mínimo para poder descargarlo desde SeaweedFS.</summary>
    public Task<ArchivoDeCarga?> ArchivoAsync(int idCarga, CancellationToken ct = default) =>
        consulta.ArchivoAsync(idCarga, ct);
}

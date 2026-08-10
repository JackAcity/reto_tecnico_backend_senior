using Auth.Domain;

namespace Auth.Api;

/// <summary>
/// Puerto de acceso a datos de <see cref="ServicioAutenticacion"/> (design.md
/// §D2 de arquitectura-hexagonal-transversal): Application no conoce EF Core.
/// </summary>
public interface IRepositorioUsuarios
{
    Task<Usuario?> ObtenerPorEmailActivoAsync(string email, CancellationToken ct);
    Task<RefreshToken?> ObtenerRefreshTokenConUsuarioAsync(string token, CancellationToken ct);

    /// <summary>
    /// Revoca el refresh token si sigue activo (§C18: compare-and-swap vía
    /// <c>WHERE revocado_en IS NULL</c>). Devuelve las filas afectadas — 0
    /// significa que otro request ya lo rotó primero.
    /// </summary>
    Task<int> RevocarSiActivoAsync(int idToken, string reemplazadoPor, DateTimeOffset ahora, CancellationToken ct);

    void AgregarRefreshToken(RefreshToken token);
    Task GuardarCambiosAsync(CancellationToken ct);
}

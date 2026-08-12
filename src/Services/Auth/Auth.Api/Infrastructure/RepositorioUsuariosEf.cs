using Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api;

/// <summary>Adaptador EF de <see cref="IRepositorioUsuarios"/> (design.md §D2).</summary>
public sealed class RepositorioUsuariosEf(AuthDbContext db) : IRepositorioUsuarios
{
    public Task<Usuario?> ObtenerPorEmailActivoAsync(string email, CancellationToken ct) =>
        db.Usuarios.SingleOrDefaultAsync(u => u.Email == email && u.Activo, ct);

    public Task<RefreshToken?> ObtenerRefreshTokenConUsuarioAsync(string token, CancellationToken ct) =>
        db.RefreshTokens.Include(t => t.Usuario).AsNoTracking().SingleOrDefaultAsync(t => t.Token == token, ct);

    public Task<int> RevocarSiActivoAsync(int idToken, string reemplazadoPor, DateTimeOffset ahora, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.Id == idToken && t.RevocadoEn == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevocadoEn, ahora)
                .SetProperty(t => t.ReemplazadoPor, reemplazadoPor), ct);

    public void AgregarRefreshToken(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

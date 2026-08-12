using Auth.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Api;

/// <summary>
/// Modelo EF privado de Auth. La base física sigue siendo común por el enunciado,
/// pero sus detalles de persistencia no son un contrato para otros servicios.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Usuario>(entidad =>
        {
            entidad.ToTable("usuario");
            entidad.Property(usuario => usuario.Email).HasMaxLength(150).IsRequired();
            entidad.Property(usuario => usuario.PasswordHash).IsRequired();
            entidad.Property(usuario => usuario.Rol).HasMaxLength(50).IsRequired();
            entidad.Property(usuario => usuario.Activo).HasDefaultValue(true);
            entidad.HasIndex(usuario => usuario.Email).IsUnique();
        });

        modelo.Entity<RefreshToken>(entidad =>
        {
            entidad.ToTable("refresh_token");
            entidad.Property(token => token.Token).HasMaxLength(200).IsRequired();
            entidad.Property(token => token.ReemplazadoPor).HasMaxLength(200);
            entidad.HasIndex(token => token.Token).IsUnique();
            entidad.HasOne(token => token.Usuario).WithMany().HasForeignKey(token => token.UsuarioId);
        });
    }
}

public static class PersistenciaAuthExtensiones
{
    /// <summary>Registra exclusivamente el adaptador de datos que pertenece a Auth.</summary>
    public static IServiceCollection AddPersistenciaAuth(this IServiceCollection servicios, string? cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        return servicios.AddDbContext<AuthDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion)
            .UseSnakeCaseNamingConvention());
    }

    /// <summary>
    /// Siembra una cuenta de acceso de forma idempotente. El índice único es la
    /// autoridad ante dos réplicas de Auth arrancando a la vez.
    /// </summary>
    public static async Task<bool> SembrarUsuarioAsync(
        this AuthDbContext db, string email, string password, string rol, CancellationToken ct = default)
    {
        if (await db.Usuarios.AnyAsync(usuario => usuario.Email == email, ct))
            return false;

        var usuario = new Usuario { Email = email, Rol = rol, Activo = true };
        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, password);
        db.Usuarios.Add(usuario);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(usuario).State = EntityState.Detached;
            return false;
        }
    }
}
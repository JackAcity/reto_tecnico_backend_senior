using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Notificaciones.Api;

/// <summary>Adaptador EF privado de Notificaciones para el estado que administra.</summary>
public sealed class NotificacionesDbContext(DbContextOptions<NotificacionesDbContext> options) : DbContext(options)
{
    public DbSet<CargaPorNotificar> CargaArchivos => Set<CargaPorNotificar>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<CargaPorNotificar>(entidad =>
        {
            entidad.ToTable("carga_archivo");
            entidad.Property(carga => carga.Usuario).HasMaxLength(150).IsRequired();
            entidad.Property(carga => carga.Estado).HasMaxLength(20).HasConversion<string>().IsRequired();
        });
    }
}

public static class PersistenciaNotificacionesExtensiones
{
    /// <summary>Registra sólo la persistencia que requiere Notificaciones.</summary>
    public static IServiceCollection AddPersistenciaNotificaciones(this IServiceCollection servicios, string? cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        return servicios.AddDbContext<NotificacionesDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion)
            .UseSnakeCaseNamingConvention());
    }
}
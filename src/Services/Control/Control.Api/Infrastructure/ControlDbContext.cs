using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Control.Api;

/// <summary>Adaptador EF privado de Control; una base común no crea un contrato común de objetos.</summary>
public sealed class ControlDbContext(DbContextOptions<ControlDbContext> options) : DbContext(options)
{
    public DbSet<RegistroCarga> CargaArchivos => Set<RegistroCarga>();
    public DbSet<PeriodoRegistroCarga> CargaPeriodos => Set<PeriodoRegistroCarga>();
    public DbSet<ErrorRegistroCarga> DetalleCargaErrores => Set<ErrorRegistroCarga>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<RegistroCarga>(entidad =>
        {
            entidad.ToTable("carga_archivo");
            entidad.Property(carga => carga.NombreArchivo).HasMaxLength(260).IsRequired();
            entidad.Property(carga => carga.RutaArchivo).HasMaxLength(500);
            entidad.Property(carga => carga.Usuario).HasMaxLength(150).IsRequired();
            entidad.Property(carga => carga.Estado).HasMaxLength(20).HasConversion<string>().IsRequired();
            entidad.Property(carga => carga.CorrelationId).HasMaxLength(50).IsRequired();
            entidad.HasIndex(carga => carga.Estado);
        });

        modelo.Entity<PeriodoRegistroCarga>(entidad =>
        {
            entidad.ToTable("carga_periodo");
            entidad.Property(periodo => periodo.Periodo).HasMaxLength(7).IsRequired();
            entidad.Property(periodo => periodo.Estado).HasMaxLength(20).IsRequired();
            entidad.HasOne(periodo => periodo.CargaArchivo).WithMany(carga => carga.Periodos).HasForeignKey(periodo => periodo.CargaArchivoId);
        });

        modelo.Entity<ErrorRegistroCarga>(entidad =>
        {
            entidad.ToTable("detalle_carga_error");
            entidad.Property(error => error.Periodo).HasMaxLength(7);
            entidad.Property(error => error.CodigoProducto).HasMaxLength(50);
            entidad.Property(error => error.Columna).HasMaxLength(50);
            entidad.Property(error => error.Motivo).HasMaxLength(40).IsRequired();
            entidad.HasIndex(error => error.CargaArchivoId);
        });
    }
}

public static class PersistenciaControlExtensiones
{
    /// <summary>Registra únicamente la persistencia que pertenece a Control.</summary>
    public static IServiceCollection AddPersistenciaControl(this IServiceCollection servicios, string? cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        return servicios.AddDbContext<ControlDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion)
            .UseSnakeCaseNamingConvention());
    }
}
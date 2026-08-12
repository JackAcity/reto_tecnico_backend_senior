using CargaMasiva.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// Modelo EF privado de CargaMasiva. Comparte una base por requisito del reto,
/// no un ensamblado ni un contexto con los demás bounded contexts.
/// </summary>
public sealed class CargaMasivaDbContext(DbContextOptions<CargaMasivaDbContext> options) : DbContext(options)
{
    public DbSet<CargaArchivo> CargaArchivos => Set<CargaArchivo>();
    public DbSet<CargaPeriodo> CargaPeriodos => Set<CargaPeriodo>();
    public DbSet<DataProcesada> DataProcesadas => Set<DataProcesada>();
    public DbSet<DetalleCargaError> DetalleCargaErrores => Set<DetalleCargaError>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<CargaArchivo>(entidad =>
        {
            entidad.ToTable("carga_archivo");
            entidad.Property(carga => carga.NombreArchivo).HasMaxLength(260).IsRequired();
            entidad.Property(carga => carga.RutaArchivo).HasMaxLength(500);
            entidad.Property(carga => carga.Usuario).HasMaxLength(150).IsRequired();
            entidad.Property(carga => carga.Estado).HasMaxLength(20).HasConversion<string>().IsRequired();
            entidad.Property(carga => carga.CorrelationId).HasMaxLength(50).IsRequired();
            entidad.Property(carga => carga.TotalFilas).HasDefaultValue(0);
            entidad.Property(carga => carga.FilasInsertadas).HasDefaultValue(0);
            entidad.Property(carga => carga.FilasRechazadas).HasDefaultValue(0);
            entidad.HasIndex(carga => carga.Estado);
        });

        modelo.Entity<CargaPeriodo>(entidad =>
        {
            entidad.ToTable("carga_periodo");
            entidad.Property(periodo => periodo.Periodo).HasMaxLength(7).IsRequired();
            entidad.Property(periodo => periodo.Estado).HasMaxLength(20).IsRequired();
            entidad.Property(periodo => periodo.FilasInsertadas).HasDefaultValue(0);
            entidad.HasOne(periodo => periodo.CargaArchivo).WithMany(carga => carga.Periodos).HasForeignKey(periodo => periodo.CargaArchivoId);
            entidad.HasIndex(periodo => periodo.Periodo)
                .IsUnique()
                .HasFilter($"estado = '{EstadoPeriodo.Aceptado}'")
                .HasDatabaseName("ux_carga_periodo_activo");
        });

        modelo.Entity<DataProcesada>(entidad =>
        {
            entidad.ToTable("data_procesada");
            entidad.Property(dato => dato.Periodo).HasMaxLength(7).IsRequired();
            entidad.Property(dato => dato.CodigoProducto).HasMaxLength(50).IsRequired();
            entidad.Property(dato => dato.NombreProducto).HasMaxLength(200).IsRequired();
            entidad.Property(dato => dato.Precio).HasPrecision(18, 2);
            entidad.HasOne(dato => dato.CargaArchivo).WithMany().HasForeignKey(dato => dato.CargaArchivoId);
            entidad.HasIndex(dato => new { dato.Periodo, dato.CodigoProducto })
                .IsUnique()
                .HasDatabaseName("ux_data_procesada_periodo_codigo");
        });

        modelo.Entity<DetalleCargaError>(entidad =>
        {
            entidad.ToTable("detalle_carga_error");
            entidad.Property(error => error.Periodo).HasMaxLength(7);
            entidad.Property(error => error.CodigoProducto).HasMaxLength(50);
            entidad.Property(error => error.Columna).HasMaxLength(50);
            entidad.Property(error => error.Motivo).HasMaxLength(40).HasConversion<string>().IsRequired();
            entidad.HasOne(error => error.CargaArchivo).WithMany().HasForeignKey(error => error.CargaArchivoId);
            entidad.HasIndex(error => error.CargaArchivoId);
        });
    }
}

public static class PersistenciaCargaMasivaExtensiones
{
    /// <summary>Registra exclusivamente el adaptador EF que pertenece a CargaMasiva.</summary>
    public static IServiceCollection AddPersistenciaCargaMasiva(this IServiceCollection servicios, string? cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        return servicios.AddDbContext<CargaMasivaDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion)
            .UseSnakeCaseNamingConvention());
    }
}
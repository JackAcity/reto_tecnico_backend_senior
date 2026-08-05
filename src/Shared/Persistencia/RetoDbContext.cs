using CargaMasiva.Domain;
using Microsoft.EntityFrameworkCore;

namespace Persistencia;

/// <summary>
/// Una sola base, un solo esquema: el diagrama entregado lo prescribe (design.md §C10).
/// La propiedad de escritura por tabla está declarada en db-schema.md y se respeta por
/// disciplina, no por límite físico.
/// </summary>
public sealed class RetoDbContext(DbContextOptions<RetoDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CargaArchivo> CargaArchivos => Set<CargaArchivo>();
    public DbSet<CargaPeriodo> CargaPeriodos => Set<CargaPeriodo>();
    public DbSet<DataProcesada> DataProcesadas => Set<DataProcesada>();
    public DbSet<DetalleCargaError> DetalleCargaErrores => Set<DetalleCargaError>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Usuario>(e =>
        {
            e.ToTable("usuario");
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Rol).HasMaxLength(50).IsRequired();
            e.Property(u => u.Activo).HasDefaultValue(true);
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelo.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.Property(t => t.Token).HasMaxLength(200).IsRequired();
            e.Property(t => t.ReemplazadoPor).HasMaxLength(200);
            e.HasIndex(t => t.Token).IsUnique();
            e.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId);
        });

        modelo.Entity<CargaArchivo>(e =>
        {
            e.ToTable("carga_archivo");
            e.Property(c => c.NombreArchivo).HasMaxLength(260).IsRequired();
            e.Property(c => c.RutaArchivo).HasMaxLength(500);
            e.Property(c => c.Usuario).HasMaxLength(150).IsRequired();
            // Estado como texto: legible en un SELECT manual durante la demo, y estable
            // si mañana se agrega un estado en medio del enum.
            e.Property(c => c.Estado).HasMaxLength(20).HasConversion<string>().IsRequired();
            e.Property(c => c.CorrelationId).HasMaxLength(50).IsRequired();
            // Contadores con default en el motor: un INSERT manual desde psql (o desde
            // scripts/sql) no tiene que enumerarlos. db-schema.md los declara DEFAULT 0.
            e.Property(c => c.TotalFilas).HasDefaultValue(0);
            e.Property(c => c.FilasInsertadas).HasDefaultValue(0);
            e.Property(c => c.FilasRechazadas).HasDefaultValue(0);
            e.HasIndex(c => c.Estado);
        });

        modelo.Entity<CargaPeriodo>(e =>
        {
            e.ToTable("carga_periodo");
            e.Property(p => p.Periodo).HasMaxLength(7).IsRequired();
            e.Property(p => p.Estado).HasMaxLength(20).IsRequired();
            e.Property(p => p.FilasInsertadas).HasDefaultValue(0);
            e.HasOne(p => p.CargaArchivo).WithMany(c => c.Periodos).HasForeignKey(p => p.CargaArchivoId);

            // Impide dos cargas activas del mismo periodo a nivel motor. Un SELECT previo
            // al INSERT sería un TOCTOU (design.md §C9).
            e.HasIndex(p => p.Periodo)
                .IsUnique()
                .HasFilter($"estado = '{EstadoPeriodo.Aceptado}'")
                .HasDatabaseName("ux_carga_periodo_activo");
        });

        modelo.Entity<DataProcesada>(e =>
        {
            e.ToTable("data_procesada");
            e.Property(d => d.Periodo).HasMaxLength(7).IsRequired();
            e.Property(d => d.CodigoProducto).HasMaxLength(50).IsRequired();
            e.Property(d => d.NombreProducto).HasMaxLength(200).IsRequired();
            e.Property(d => d.Precio).HasPrecision(18, 2);
            e.HasOne(d => d.CargaArchivo).WithMany().HasForeignKey(d => d.CargaArchivoId);

            // La clave de negocio (design.md §C5). Sirve además como llave natural de
            // idempotencia del consumidor (§C8): reprocesar un mensaje no duplica.
            e.HasIndex(d => new { d.Periodo, d.CodigoProducto })
                .IsUnique()
                .HasDatabaseName("ux_data_procesada_periodo_codigo");
        });

        modelo.Entity<DetalleCargaError>(e =>
        {
            e.ToTable("detalle_carga_error");
            e.Property(d => d.Periodo).HasMaxLength(7);
            e.Property(d => d.CodigoProducto).HasMaxLength(50);
            e.Property(d => d.Columna).HasMaxLength(50);
            e.Property(d => d.Motivo).HasMaxLength(40).HasConversion<string>().IsRequired();
            e.HasOne(d => d.CargaArchivo).WithMany().HasForeignKey(d => d.CargaArchivoId);
            e.HasIndex(d => d.CargaArchivoId);
        });
    }
}

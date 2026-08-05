using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Persistencia;

public static class PersistenciaExtensiones
{
    /// <summary>Registra el DbContext. La convención snake_case evita mapear columna por columna.</summary>
    public static IServiceCollection AddPersistencia(this IServiceCollection servicios, string? cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        return servicios.AddDbContext<RetoDbContext>(o => o
            .UseNpgsql(cadenaConexion)
            .UseSnakeCaseNamingConvention());
    }

    /// <summary>
    /// §4.14 — migraciones automáticas al arranque. La llama <b>solo Control</b>: cinco
    /// servicios migrando a la vez es una carrera (design.md §C11); el resto espera por
    /// health check.
    /// </summary>
    public static async Task MigrarAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<RetoDbContext>().Database.MigrateAsync(ct);
    }
}

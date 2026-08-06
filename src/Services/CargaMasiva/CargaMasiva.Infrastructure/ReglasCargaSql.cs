using CargaMasiva.Application;
using Npgsql;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// Implementa el puerto que <see cref="ProcesadorLote"/> define, contra los dos
/// procedimientos almacenados (§4.15) y la clave de negocio (design.md §C5).
/// Conexión propia por llamada, no la de EF: <c>sp_resolver_periodo</c> necesita
/// su propia transacción para que <c>pg_advisory_xact_lock</c> libere al hacer
/// commit — exactamente el patrón ya probado en EsquemaPostgresTests.
/// </summary>
public sealed class ReglasCargaSql(string cadenaConexion) : IReglasCarga
{
    public async Task<ResultadoPeriodo> ResolverPeriodoAsync(int idCarga, string periodo, CancellationToken ct)
    {
        await using var cn = new NpgsqlConnection(cadenaConexion);
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);

        await using var cmd = new NpgsqlCommand("SELECT sp_resolver_periodo(@id, @periodo::varchar);", cn, tx);
        cmd.Parameters.AddWithValue("id", idCarga);
        cmd.Parameters.AddWithValue("periodo", periodo);
        var texto = (string)(await cmd.ExecuteScalarAsync(ct))!;

        await tx.CommitAsync(ct);

        // El nombre que devuelve el SP debe coincidir con un miembro de
        // ResultadoPeriodo — es el mismo vocabulario, verificado en EsquemaPostgresTests.
        return Enum.Parse<ResultadoPeriodo>(texto);
    }

    public async Task<IReadOnlySet<ClaveProducto>> ObtenerExistentesAsync(
        IReadOnlyCollection<ClaveProducto> claves, CancellationToken ct)
    {
        if (claves.Count == 0)
            return new HashSet<ClaveProducto>();

        await using var cn = new NpgsqlConnection(cadenaConexion);
        await cn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("""
            SELECT dp.periodo, dp.codigo_producto
              FROM data_procesada dp
              JOIN unnest(@periodos::varchar[], @codigos::varchar[]) AS t(periodo, codigo)
                ON t.periodo = dp.periodo AND t.codigo = dp.codigo_producto;
            """, cn);
        cmd.Parameters.AddWithValue("periodos", claves.Select(c => c.Periodo).ToArray());
        cmd.Parameters.AddWithValue("codigos", claves.Select(c => c.CodigoProducto).ToArray());

        var resultado = new HashSet<ClaveProducto>();
        await using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            resultado.Add(new ClaveProducto(lector.GetString(0), lector.GetString(1)));

        return resultado;
    }
}

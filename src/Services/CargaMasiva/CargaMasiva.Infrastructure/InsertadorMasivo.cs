using CargaMasiva.Domain;
using Npgsql;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// Wrapper de <c>sp_insertar_data_procesada</c> (§4.15): inserción set-based con
/// <c>unnest</c>, un round trip en vez de N. La cuenta devuelta por el motor —no
/// <c>filas.Count</c>— es la verdad: la diferencia son duplicados que ya existían
/// en base pese a que <see cref="IReglasCarga.ObtenerExistentesAsync"/> ya los
/// filtró (una carga concurrente pudo insertarlos justo en el medio).
/// </summary>
public sealed class InsertadorMasivo(string cadenaConexion)
{
    public async Task<int> InsertarAsync(int idCarga, IReadOnlyList<FilaProducto> filas, CancellationToken ct)
    {
        if (filas.Count == 0)
            return 0;

        await using var cn = new NpgsqlConnection(cadenaConexion);
        await cn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("""
            SELECT sp_insertar_data_procesada(
                @id, @periodos::varchar[], @codigos::varchar[], @nombres::varchar[], @precios::numeric[]);
            """, cn);
        cmd.Parameters.AddWithValue("id", idCarga);
        cmd.Parameters.AddWithValue("periodos", filas.Select(f => f.Periodo).ToArray());
        cmd.Parameters.AddWithValue("codigos", filas.Select(f => f.CodigoProducto).ToArray());
        cmd.Parameters.AddWithValue("nombres", filas.Select(f => f.NombreProducto).ToArray());
        cmd.Parameters.AddWithValue("precios", filas.Select(f => f.Precio).ToArray());

        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }
}

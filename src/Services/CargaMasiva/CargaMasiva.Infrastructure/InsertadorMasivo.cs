using CargaMasiva.Domain;
using Npgsql;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// Wrapper de <c>sp_insertar_data_procesada</c> (§4.15): inserción set-based con
/// <c>unnest</c>, por lotes de <see cref="TamanoLote"/> en vez de un único round
/// trip con el archivo entero. Medido contra un archivo real de 2M filas: un solo
/// <c>unnest</c> para todas agota el <c>CommandTimeout</c> de Npgsql (carga queda
/// <c>Fallida</c> tras 3 reintentos, ver docs/pruebas-de-escala.md) — el timeout
/// no es transitorio, así que reintentar el mismo round trip gigante no ayuda.
/// La cuenta devuelta por el motor —no <c>filas.Count</c>— es la verdad: la
/// diferencia son duplicados que ya existían en base pese a que
/// <see cref="IReglasCarga.ObtenerExistentesAsync"/> ya los filtró (una carga
/// concurrente pudo insertarlos justo en el medio).
/// </summary>
public sealed class InsertadorMasivo(string cadenaConexion, int tamanoLote = 20_000)
{
    public async Task<int> InsertarAsync(int idCarga, IReadOnlyList<FilaProducto> filas, CancellationToken ct)
    {
        if (filas.Count == 0)
            return 0;

        await using var cn = new NpgsqlConnection(cadenaConexion);
        await cn.OpenAsync(ct);

        var insertadas = 0;
        for (var inicio = 0; inicio < filas.Count; inicio += tamanoLote)
        {
            var cantidad = Math.Min(tamanoLote, filas.Count - inicio);
            insertadas += await InsertarLoteAsync(cn, idCarga, filas, inicio, cantidad, ct);
        }
        return insertadas;
    }

    private static async Task<int> InsertarLoteAsync(
        NpgsqlConnection cn, int idCarga, IReadOnlyList<FilaProducto> filas, int inicio, int cantidad, CancellationToken ct)
    {
        var periodos = new string[cantidad];
        var codigos = new string[cantidad];
        var nombres = new string[cantidad];
        var precios = new decimal[cantidad];
        for (var i = 0; i < cantidad; i++)
        {
            var fila = filas[inicio + i];
            periodos[i] = fila.Periodo;
            codigos[i] = fila.CodigoProducto;
            nombres[i] = fila.NombreProducto;
            precios[i] = fila.Precio;
        }

        await using var cmd = new NpgsqlCommand("""
            SELECT sp_insertar_data_procesada(
                @id, @periodos::varchar[], @codigos::varchar[], @nombres::varchar[], @precios::numeric[]);
            """, cn);
        cmd.Parameters.AddWithValue("id", idCarga);
        cmd.Parameters.AddWithValue("periodos", periodos);
        cmd.Parameters.AddWithValue("codigos", codigos);
        cmd.Parameters.AddWithValue("nombres", nombres);
        cmd.Parameters.AddWithValue("precios", precios);

        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }
}

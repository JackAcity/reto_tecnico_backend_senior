using CargaMasiva.Application;
using CargaMasiva.Domain;
using Npgsql;

namespace CargaMasiva.Tests;

/// <summary>
/// Prueba de integración del Bloque 2: valida los dos procedimientos almacenados
/// (§4.15) y la clave de negocio contra un PostgreSQL real.
///
/// Requiere la base levantada: <c>docker compose up -d postgres</c> + migraciones
/// aplicadas (las aplica Control al arrancar). Todo corre dentro de una transacción
/// que se revierte al final, así que es repetible sin dejar rastro.
/// </summary>
public sealed class EsquemaPostgresTests : IAsyncLifetime
{
    private const string Periodo = "2099-01";   // fuera del rango de cualquier dato real

    private static string Cadena =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Database=reto;Username=reto;Password=cambiar_en_local";

    private NpgsqlConnection _cn = null!;
    private NpgsqlTransaction _tx = null!;

    public async Task InitializeAsync()
    {
        _cn = new NpgsqlConnection(Cadena);
        await _cn.OpenAsync();
        _tx = await _cn.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _cn.DisposeAsync();
    }

    // El estado se persiste como el NOMBRE del enum (HasConversion<string>(),
    // RetoDbContext.cs): pasar el enum y convertir acá adentro es la única forma
    // de que un rename del enum rompa la compilación en vez de romper en silencio
    // un literal "EnProceso" suelto en el test.
    private async Task<int> NuevaCargaAsync(EstadoCarga estado)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO carga_archivo
                (nombre_archivo, tamano_bytes, usuario, fecha_registro, estado, correlation_id)
            VALUES ('prueba.xlsx', 1024, 'test@reto.local', now(), @estado, 'test')
            RETURNING id;
            """, _cn, _tx);
        cmd.Parameters.AddWithValue("estado", estado.ToString());
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// El SP devuelve un string plano (SQL no conoce enums de C#). El nombre
    /// devuelto debe coincidir con un miembro de <see cref="ResultadoPeriodo"/> —
    /// es el mismo vocabulario que usará el consumidor de CargaMasiva (Bloque 6)
    /// para interpretar el veredicto, así que se valida acá con <c>Enum.Parse</c>
    /// en vez de comparar el string crudo.
    /// </summary>
    private async Task<ResultadoPeriodo> ResolverPeriodoAsync(int idCarga, string periodo = Periodo)
    {
        await using var cmd = new NpgsqlCommand("SELECT sp_resolver_periodo(@id, @periodo::varchar);", _cn, _tx);
        cmd.Parameters.AddWithValue("id", idCarga);
        cmd.Parameters.AddWithValue("periodo", periodo);
        var texto = (string)(await cmd.ExecuteScalarAsync())!;
        return Enum.Parse<ResultadoPeriodo>(texto);
    }

    private async Task CambiarEstadoAsync(int idCarga, EstadoCarga estado)
    {
        await using var cmd = new NpgsqlCommand("UPDATE carga_archivo SET estado = @estado WHERE id = @id;", _cn, _tx);
        cmd.Parameters.AddWithValue("estado", estado.ToString());
        cmd.Parameters.AddWithValue("id", idCarga);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int> InsertarLoteAsync(int idCarga, string[] periodos, string[] codigos, string[] nombres, decimal[] precios)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT sp_insertar_data_procesada(
                @id, @periodos::varchar[], @codigos::varchar[], @nombres::varchar[], @precios::numeric[]);
            """, _cn, _tx);
        cmd.Parameters.AddWithValue("id", idCarga);
        cmd.Parameters.AddWithValue("periodos", periodos);
        cmd.Parameters.AddWithValue("codigos", codigos);
        cmd.Parameters.AddWithValue("nombres", nombres);
        cmd.Parameters.AddWithValue("precios", precios);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>§C2 — la carga no puede bloquearse a sí misma: el SP excluye su propio IdCarga.</summary>
    [Fact]
    public async Task PropiaCarga_NoSeAutoBloquea_NiSiquieraAlReintentar()
    {
        var carga = await NuevaCargaAsync(EstadoCarga.EnProceso);

        Assert.Equal(ResultadoPeriodo.Libre, await ResolverPeriodoAsync(carga));
        // Reentrega del mismo mensaje (§C8): sigue siendo Libre, sin fila duplicada.
        Assert.Equal(ResultadoPeriodo.Libre, await ResolverPeriodoAsync(carga));
    }

    /// <summary>§3.3 — otra carga en vuelo sobre el mismo periodo bloquea.</summary>
    [Fact]
    public async Task OtraCargaEnProceso_BloqueaElPeriodo()
    {
        var primera = await NuevaCargaAsync(EstadoCarga.EnProceso);
        await ResolverPeriodoAsync(primera);

        var segunda = await NuevaCargaAsync(EstadoCarga.EnProceso);

        Assert.Equal(ResultadoPeriodo.Bloqueado, await ResolverPeriodoAsync(segunda));
    }

    /// <summary>§3.3 — periodo ya cargado: se rechaza, con motivo distinto al bloqueo.</summary>
    [Fact]
    public async Task PeriodoYaFinalizado_DevuelveYaCargado()
    {
        var primera = await NuevaCargaAsync(EstadoCarga.EnProceso);
        await ResolverPeriodoAsync(primera);
        await CambiarEstadoAsync(primera, EstadoCarga.Finalizado);

        var segunda = await NuevaCargaAsync(EstadoCarga.EnProceso);

        Assert.Equal(ResultadoPeriodo.YaCargado, await ResolverPeriodoAsync(segunda));
    }

    /// <summary>Una carga que murió no puede reservar el periodo para siempre.</summary>
    [Fact]
    public async Task CargaFallida_LiberaElPeriodo()
    {
        var primera = await NuevaCargaAsync(EstadoCarga.EnProceso);
        await ResolverPeriodoAsync(primera);
        await CambiarEstadoAsync(primera, EstadoCarga.Fallida);

        var segunda = await NuevaCargaAsync(EstadoCarga.EnProceso);

        Assert.Equal(ResultadoPeriodo.Libre, await ResolverPeriodoAsync(segunda));
    }

    /// <summary>
    /// La decisión de design.md §C5 verificada en el motor: el mismo código en dos
    /// periodos son dos filas; repetido dentro del periodo, una sola.
    /// </summary>
    [Fact]
    public async Task InsercionMasiva_ClaveEsPeriodoMasCodigo()
    {
        var carga = await NuevaCargaAsync(EstadoCarga.EnProceso);

        var insertadas = await InsertarLoteAsync(carga,
            periodos: [Periodo, "2099-02", Periodo],
            codigos:  ["P0001", "P0001",   "P0001"],
            nombres:  ["Uno",   "Dos",     "Duplicado"],
            precios:  [10.50m,  20.00m,    99.99m]);

        // Dos pares distintos entran; el tercero choca con el primero y se ignora.
        Assert.Equal(2, insertadas);
    }

    /// <summary>§C8 — reprocesar un lote ya insertado no duplica ni lanza.</summary>
    [Fact]
    public async Task InsercionMasiva_ReprocesarNoDuplica()
    {
        var carga = await NuevaCargaAsync(EstadoCarga.EnProceso);
        string[] periodos = [Periodo];
        string[] codigos = ["P0500"];
        string[] nombres = ["Producto"];
        decimal[] precios = [15m];

        Assert.Equal(1, await InsertarLoteAsync(carga, periodos, codigos, nombres, precios));
        Assert.Equal(0, await InsertarLoteAsync(carga, periodos, codigos, nombres, precios));
    }
}

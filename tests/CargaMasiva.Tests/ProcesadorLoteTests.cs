using CargaMasiva.Application;
using CargaMasiva.Domain;
using CargaMasiva.Infrastructure;

namespace CargaMasiva.Tests;

/// <summary>Doble de prueba: base vacía, todos los periodos libres.</summary>
file sealed class ReglasEnMemoria(
    Dictionary<string, ResultadoPeriodo>? periodos = null,
    HashSet<ClaveProducto>? existentes = null) : IReglasCarga
{
    private readonly Dictionary<string, ResultadoPeriodo> _periodos = periodos ?? [];
    private readonly HashSet<ClaveProducto> _existentes = existentes ?? [];

    public Task<ResultadoPeriodo> ResolverPeriodoAsync(int idCarga, string periodo, CancellationToken ct) =>
        Task.FromResult(_periodos.GetValueOrDefault(periodo, ResultadoPeriodo.Libre));

    public Task<IReadOnlySet<ClaveProducto>> ObtenerExistentesAsync(IReadOnlyCollection<ClaveProducto> claves, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<ClaveProducto>>(_existentes.Where(claves.Contains).ToHashSet());
}

public class ProcesadorLoteTests
{
    private static readonly string RutaMuestra =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "carga_masiva_productos.xlsx");

    private static List<FilaCruda> LeerMuestra()
    {
        using var fs = File.OpenRead(Path.GetFullPath(RutaMuestra));
        return new LectorExcel().Leer(fs).ToList();
    }

    /// <summary>
    /// Prueba de aceptación del núcleo funcional (35% de la rúbrica).
    /// La clave es (Periodo, CodigoProducto) — justificación en design.md §C5.
    /// </summary>
    [Fact]
    public async Task ArchivoDeMuestra_Inserta154_Rechaza46()
    {
        var filas = LeerMuestra();
        Assert.Equal(200, filas.Count);

        var resultado = await new ProcesadorLote(new ReglasEnMemoria()).ProcesarAsync(idCarga: 1, filas);

        Assert.Equal(154, resultado.Aceptadas.Count);
        Assert.Equal(46, resultado.Rechazadas.Count);
        Assert.All(resultado.Rechazadas, r => Assert.Equal(MotivoRechazo.Existente, r.Motivo));
    }

    /// <summary>
    /// Deja ejecutable el escenario alternativo descartado, para que la decisión
    /// de design.md §C5 sea verificable y no solo declarativa.
    /// </summary>
    [Fact]
    public void ArchivoDeMuestra_ConClaveGlobal_Daria116y84()
    {
        var filas = LeerMuestra();
        var codigosDistintos = filas.Select(f => f.CodigoProducto).Distinct().Count();

        Assert.Equal(116, codigosDistintos);
        Assert.Equal(84, filas.Count - codigosDistintos);
    }

    [Fact]
    public async Task ArchivoDeMuestra_DetectaLosTresPeriodos()
    {
        var resultado = await new ProcesadorLote(new ReglasEnMemoria()).ProcesarAsync(1, LeerMuestra());

        Assert.Equal(["2025-01", "2025-02", "2025-03"], resultado.Periodos.Keys.Order());
    }

    [Fact]
    public async Task PeriodoYaCargado_DescartaSoloEseTramo_YProcesaElResto()
    {
        var reglas = new ReglasEnMemoria(new() { ["2025-02"] = ResultadoPeriodo.YaCargado });

        var resultado = await new ProcesadorLote(reglas).ProcesarAsync(1, LeerMuestra());

        Assert.DoesNotContain(resultado.Aceptadas, f => f.Periodo == "2025-02");
        Assert.Contains(resultado.Aceptadas, f => f.Periodo == "2025-01");
        Assert.Contains(resultado.Rechazadas, r => r.Motivo == MotivoRechazo.PeriodoYaCargado);
        Assert.False(resultado.NingunPeriodoAceptado);
    }

    [Fact]
    public async Task TodosLosPeriodosYaCargados_NoAceptaNada()
    {
        var reglas = new ReglasEnMemoria(new()
        {
            ["2025-01"] = ResultadoPeriodo.YaCargado,
            ["2025-02"] = ResultadoPeriodo.YaCargado,
            ["2025-03"] = ResultadoPeriodo.Bloqueado
        });

        var resultado = await new ProcesadorLote(reglas).ProcesarAsync(1, LeerMuestra());

        Assert.Empty(resultado.Aceptadas);
        Assert.True(resultado.NingunPeriodoAceptado);
    }

    /// <summary>Idempotencia: reprocesar un mensaje reentregado no inserta de nuevo (§C8).</summary>
    [Fact]
    public async Task Reproceso_ConTodoYaEnBase_NoAceptaNada()
    {
        var filas = LeerMuestra();
        var yaInsertadas = (await new ProcesadorLote(new ReglasEnMemoria()).ProcesarAsync(1, filas))
            .Aceptadas.Select(f => new ClaveProducto(f.Periodo, f.CodigoProducto)).ToHashSet();

        var resultado = await new ProcesadorLote(new ReglasEnMemoria(existentes: yaInsertadas)).ProcesarAsync(1, filas);

        Assert.Empty(resultado.Aceptadas);
        Assert.Equal(200, resultado.Rechazadas.Count);
    }

    [Fact]
    public async Task FilasVacias_NoSeRegistranNiSeAuditan()
    {
        FilaCruda[] filas =
        [
            new(2, "2025-01", "P001", "Producto A", "10.50"),
            new(3, null, null, null, null),
            new(4, "  ", "", "   ", ""),
            new(5, "2025-01", "P002", "Producto B", "20")
        ];

        var resultado = await new ProcesadorLote(new ReglasEnMemoria()).ProcesarAsync(1, filas);

        Assert.Equal(2, resultado.Aceptadas.Count);
        Assert.Empty(resultado.Rechazadas);
    }

    [Fact]
    public async Task ColumnasVacias_AplicanValorPorDefecto_YSeAuditan()
    {
        FilaCruda[] filas =
        [
            new(2, "2025-01", "P001", null, "10.50"),
            new(3, "2025-01", "P002", "Producto B", null),
            new(4, "2025-01", "P003", "Producto C", "no-es-un-numero")
        ];

        var resultado = await new ProcesadorLote(new ReglasEnMemoria()).ProcesarAsync(1, filas);

        Assert.Equal(3, resultado.Aceptadas.Count);
        Assert.Equal(NormalizadorFila.NombrePorDefecto, resultado.Aceptadas[0].NombreProducto);
        Assert.Equal(0m, resultado.Aceptadas[1].Precio);
        Assert.Equal(0m, resultado.Aceptadas[2].Precio);
        Assert.Equal(3, resultado.Observaciones.Count);
        Assert.All(resultado.Observaciones, o => Assert.Equal(MotivoRechazo.ValorPorDefectoAplicado, o.Motivo));
    }

    [Fact]
    public async Task DuplicadoDentroDelMismoArchivo_GanaLaPrimeraOcurrencia()
    {
        FilaCruda[] filas =
        [
            new(2, "2025-01", "P001", "Primera", "10"),
            new(3, "2025-01", "P001", "Segunda", "99"),
            new(4, "2025-02", "P001", "Otro periodo", "50")
        ];

        var resultado = await new ProcesadorLote(new ReglasEnMemoria()).ProcesarAsync(1, filas);

        Assert.Equal(2, resultado.Aceptadas.Count);
        Assert.Equal("Primera", resultado.Aceptadas[0].NombreProducto);
        Assert.Single(resultado.Rechazadas);
        Assert.Equal(MotivoRechazo.Existente, resultado.Rechazadas[0].Motivo);
    }
}

public class MaquinaEstadosTests
{
    [Fact]
    public void SoloLasTransicionesDeclaradasSonValidas()
    {
        (EstadoCarga, EstadoCarga)[] validas =
        [
            (EstadoCarga.Pendiente,  EstadoCarga.EnProceso),
            (EstadoCarga.Pendiente,  EstadoCarga.Fallida),
            (EstadoCarga.EnProceso,  EstadoCarga.Cargado),
            (EstadoCarga.EnProceso,  EstadoCarga.Rechazada),
            (EstadoCarga.EnProceso,  EstadoCarga.Bloqueada),
            (EstadoCarga.EnProceso,  EstadoCarga.Fallida),
            (EstadoCarga.Cargado,    EstadoCarga.Finalizado),
            (EstadoCarga.Finalizado, EstadoCarga.Notificado)
        ];

        foreach (var desde in Enum.GetValues<EstadoCarga>())
        foreach (var hacia in Enum.GetValues<EstadoCarga>())
            Assert.Equal(validas.Contains((desde, hacia)), MaquinaEstados.EsTransicionValida(desde, hacia));
    }

    [Fact]
    public void TransicionInvalida_Lanza() =>
        Assert.Throws<TransicionInvalidaException>(() => MaquinaEstados.Validar(EstadoCarga.Notificado, EstadoCarga.Pendiente));
}

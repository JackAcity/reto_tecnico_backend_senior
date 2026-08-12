using BuildingBlocks;
using CargaMasiva.Application;
using CargaMasiva.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace Reto.Tests;

file sealed class RepositorioCargasFalso(CargaArchivo carga) : IRepositorioCargas
{
    public List<CargaPeriodo> Periodos { get; } = [];
    public List<DetalleCargaError> Errores { get; } = [];
    public int GuardadosLlamados { get; private set; }

    public Task<CargaArchivo> ObtenerAsync(int idCarga, CancellationToken ct) => Task.FromResult(carga);

    public Task<IReadOnlyList<CargaPeriodo>> ObtenerPeriodosAsync(int idCarga, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CargaPeriodo>>(Periodos);

    public void AgregarErrores(IEnumerable<DetalleCargaError> errores) => Errores.AddRange(errores);

    public Task GuardarCambiosAsync(CancellationToken ct)
    {
        GuardadosLlamados++;
        return Task.CompletedTask;
    }
}

file sealed class AlmacenFalso : IAlmacenCarga
{
    public Task<Stream> DescargarAsync(string ruta, CancellationToken ct) =>
        Task.FromResult<Stream>(new MemoryStream());
}

file sealed class LectorExcelFalso(IReadOnlyList<FilaCruda> filas) : ILectorExcel
{
    public IEnumerable<FilaCruda> Leer(Stream stream) => filas;
}

file sealed class InsertadorFalso : IInsertadorMasivo
{
    public Task<int> InsertarAsync(int idCarga, IReadOnlyList<FilaProducto> filas, CancellationToken ct) =>
        Task.FromResult(filas.Count);
}

file sealed class ReglasEnMemoria(ResultadoPeriodo veredicto = ResultadoPeriodo.Libre) : IReglasCarga
{
    public Task<ResultadoPeriodo> ResolverPeriodoAsync(int idCarga, string periodo, CancellationToken ct) =>
        Task.FromResult(veredicto);

    public Task<IReadOnlySet<ClaveProducto>> ObtenerExistentesAsync(IReadOnlyCollection<ClaveProducto> claves, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<ClaveProducto>>(new HashSet<ClaveProducto>());
}

file sealed class PublicadorFalso : IPublicadorNotificacion
{
    public List<MensajeNotificacion> NotificacionesPublicadas { get; } = [];

    public Task<Resultado> PublicarAsync(MensajeNotificacion mensaje, string correlationId, CancellationToken ct)
    {
        NotificacionesPublicadas.Add(mensaje);
        return Task.FromResult(Resultado.Exito());
    }
}

/// <summary>
/// Cierra la Requirement "ManejadorCarga usa un puerto, no un DbContext concreto"
/// (puertos-acceso-datos) — reproduce los casos de <c>ProcesadorLoteTests</c> /
/// <c>RegistroDeCargaTests</c> sin levantar Postgres.
/// </summary>
public sealed class ManejadorCargaTests
{
    private static CargaArchivo Carga(EstadoCarga estado) => new()
    {
        Id = 1,
        NombreArchivo = "catalogo.xlsx",
        RutaArchivo = "seaweed://cargas/catalogo.xlsx",
        Usuario = "admin@reto.local",
        FechaRegistro = DateTimeOffset.UtcNow,
        Estado = estado,
        CorrelationId = "corr-test"
    };

    [Fact]
    public async Task Reentrega_DeCargaYaResuelta_NoHaceNadaYNoGuardaNiPublica()
    {
        var repositorio = new RepositorioCargasFalso(Carga(EstadoCarga.Finalizado));
        var publicador = new PublicadorFalso();
        var manejador = new ManejadorCarga(repositorio, new AlmacenFalso(), new LectorExcelFalso([]),
            new ProcesadorLote(new ReglasEnMemoria()), new InsertadorFalso(), publicador,
            NullLogger<ManejadorCarga>.Instance);

        var resultado = await manejador.ProcesarAsync(
            new MensajeCarga(1, "seaweed://x", "admin@reto.local"), "corr-1", default);

        Assert.True(resultado.EsExitoso);
        Assert.Equal(EstadoCarga.Finalizado, resultado.Valor);
        Assert.Equal(0, repositorio.GuardadosLlamados);
        Assert.Empty(publicador.NotificacionesPublicadas);
    }

    [Fact]
    public async Task PeriodoLibre_TerminaFinalizadaYPublicaNotificacion()
    {
        var repositorio = new RepositorioCargasFalso(Carga(EstadoCarga.Pendiente));
        var publicador = new PublicadorFalso();
        FilaCruda[] filas = [new(2, "2025-01", "P001", "Producto A", "10.00")];
        var manejador = new ManejadorCarga(repositorio, new AlmacenFalso(), new LectorExcelFalso(filas),
            new ProcesadorLote(new ReglasEnMemoria(ResultadoPeriodo.Libre)), new InsertadorFalso(), publicador,
            NullLogger<ManejadorCarga>.Instance);

        var resultado = await manejador.ProcesarAsync(
            new MensajeCarga(1, "seaweed://x", "admin@reto.local"), "corr-2", default);

        Assert.True(resultado.EsExitoso);
        Assert.Equal(EstadoCarga.Finalizado, resultado.Valor);
        Assert.True(repositorio.GuardadosLlamados >= 2);   // transición a EnProceso + transición final
        Assert.Single(publicador.NotificacionesPublicadas);
    }

    [Fact]
    public async Task PeriodoYaCargado_TerminaRechazadaSinPublicar()
    {
        var repositorio = new RepositorioCargasFalso(Carga(EstadoCarga.Pendiente));
        var publicador = new PublicadorFalso();
        FilaCruda[] filas = [new(2, "2025-01", "P001", "Producto A", "10.00")];
        var manejador = new ManejadorCarga(repositorio, new AlmacenFalso(), new LectorExcelFalso(filas),
            new ProcesadorLote(new ReglasEnMemoria(ResultadoPeriodo.YaCargado)), new InsertadorFalso(), publicador,
            NullLogger<ManejadorCarga>.Instance);

        var resultado = await manejador.ProcesarAsync(
            new MensajeCarga(1, "seaweed://x", "admin@reto.local"), "corr-3", default);

        Assert.True(resultado.EsExitoso);
        Assert.Equal(EstadoCarga.Rechazada, resultado.Valor);
        Assert.Empty(publicador.NotificacionesPublicadas);
    }
}

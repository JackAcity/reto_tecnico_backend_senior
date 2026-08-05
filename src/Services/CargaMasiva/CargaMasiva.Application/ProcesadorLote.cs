using CargaMasiva.Domain;

namespace CargaMasiva.Application;

public readonly record struct ClaveProducto(string Periodo, string CodigoProducto);

/// <summary>Veredicto del §3.3 para un periodo concreto del archivo.</summary>
public enum ResultadoPeriodo
{
    /// <summary>Sin cargas previas activas ni finalizadas. Se procesa.</summary>
    Libre,
    /// <summary>Ya existe una carga Cargado/Finalizado/Notificado para ese periodo.</summary>
    YaCargado,
    /// <summary>Existe otra carga Pendiente/EnProceso para ese periodo.</summary>
    Bloqueado
}

/// <summary>
/// Puerto que <see cref="ProcesadorLote"/> define y la capa de infraestructura implementa
/// (Regla de Dependencia: el dominio no conoce PostgreSQL).
/// </summary>
public interface IReglasCarga
{
    Task<ResultadoPeriodo> ResolverPeriodoAsync(int idCarga, string periodo, CancellationToken ct);
    Task<IReadOnlySet<ClaveProducto>> ObtenerExistentesAsync(IReadOnlyCollection<ClaveProducto> claves, CancellationToken ct);
}

public sealed record ResultadoProceso(
    IReadOnlyList<FilaProducto> Aceptadas,
    IReadOnlyList<FilaRechazada> Rechazadas,
    IReadOnlyList<FilaRechazada> Observaciones,
    IReadOnlyDictionary<string, ResultadoPeriodo> Periodos)
{
    public int TotalFilas => Aceptadas.Count + Rechazadas.Count;
    /// <summary>True cuando ningún periodo del archivo pudo procesarse (estado terminal Rechazada/Bloqueada).</summary>
    public bool NingunPeriodoAceptado => Periodos.Count > 0 && Periodos.Values.All(p => p != ResultadoPeriodo.Libre);
}

/// <summary>
/// Núcleo funcional del reto. Sin dependencias de Excel, base de datos ni red:
/// recibe filas crudas y devuelve el veredicto de cada una.
/// </summary>
public sealed class ProcesadorLote(IReglasCarga reglas)
{
    public async Task<ResultadoProceso> ProcesarAsync(int idCarga, IEnumerable<FilaCruda> filas, CancellationToken ct = default)
    {
        var candidatas = new List<FilaProducto>();
        var rechazadas = new List<FilaRechazada>();
        var observaciones = new List<FilaRechazada>();

        // Paso 1 — normalización. Las filas totalmente vacías desaparecen sin auditarse.
        foreach (var cruda in filas)
        {
            var r = NormalizadorFila.Normalizar(cruda);
            if (r.Descartada) { rechazadas.AddRange(r.Observaciones); continue; }
            candidatas.Add(r.Fila!);
            observaciones.AddRange(r.Observaciones);
        }

        // Paso 2 — resolución de periodos. Un archivo puede traer varios (§C3):
        // el de muestra trae 2025-01, 2025-02 y 2025-03.
        var periodos = new Dictionary<string, ResultadoPeriodo>();
        foreach (var periodo in candidatas.Select(c => c.Periodo).Distinct().Order())
            periodos[periodo] = await reglas.ResolverPeriodoAsync(idCarga, periodo, ct);

        // Paso 3 — se descartan las filas de periodos no disponibles. El procesamiento
        // es parcial: las filas de periodos libres siguen adelante.
        var vivas = new List<FilaProducto>(candidatas.Count);
        foreach (var fila in candidatas)
        {
            var veredicto = periodos[fila.Periodo];
            if (veredicto == ResultadoPeriodo.Libre) { vivas.Add(fila); continue; }

            rechazadas.Add(new FilaRechazada(fila.NumeroFila, fila.Periodo, fila.CodigoProducto,
                nameof(fila.Periodo),
                veredicto == ResultadoPeriodo.YaCargado ? MotivoRechazo.PeriodoYaCargado : MotivoRechazo.PeriodoBloqueado,
                fila.Periodo));
        }

        // Paso 4 — duplicados DENTRO del mismo archivo. El enunciado solo menciona
        // consultar la base, pero el archivo de muestra trae 46 pares repetidos (§C4).
        // Gana la primera ocurrencia.
        var vistas = new HashSet<ClaveProducto>();
        var unicas = new List<FilaProducto>(vivas.Count);
        foreach (var fila in vivas)
        {
            var clave = new ClaveProducto(fila.Periodo, fila.CodigoProducto);
            if (vistas.Add(clave)) { unicas.Add(fila); continue; }

            rechazadas.Add(new FilaRechazada(fila.NumeroFila, fila.Periodo, fila.CodigoProducto,
                nameof(fila.CodigoProducto), MotivoRechazo.Existente, fila.CodigoProducto));
        }

        // Paso 5 — duplicados ya presentes en base.
        var existentes = unicas.Count == 0
            ? (IReadOnlySet<ClaveProducto>)new HashSet<ClaveProducto>()
            : await reglas.ObtenerExistentesAsync([.. vistas], ct);

        var aceptadas = new List<FilaProducto>(unicas.Count);
        foreach (var fila in unicas)
        {
            if (!existentes.Contains(new ClaveProducto(fila.Periodo, fila.CodigoProducto)))
            {
                aceptadas.Add(fila);
                continue;
            }

            rechazadas.Add(new FilaRechazada(fila.NumeroFila, fila.Periodo, fila.CodigoProducto,
                nameof(fila.CodigoProducto), MotivoRechazo.Existente, fila.CodigoProducto));
        }

        return new ResultadoProceso(aceptadas, rechazadas, observaciones, periodos);
    }
}

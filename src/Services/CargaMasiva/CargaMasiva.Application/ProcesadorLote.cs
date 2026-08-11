using CargaMasiva.Domain;

namespace CargaMasiva.Application;

public readonly record struct ClaveProducto(string Periodo, string CodigoProducto);

/// <summary>Disponibilidad de un período para la carga en curso.</summary>
public enum ResultadoPeriodo
{
    /// <summary>No existe una carga que impida procesarlo.</summary>
    Libre,
    /// <summary>Ya fue cargado y no admite una nueva carga.</summary>
    YaCargado,
    /// <summary>Otra carga activa lo tiene reservado.</summary>
    Bloqueado
}

/// <summary>Puerto para consultar la disponibilidad de períodos y productos.</summary>
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
    /// <summary>Indica si ningún período quedó disponible para procesar.</summary>
    public bool NingunPeriodoAceptado => Periodos.Count > 0 && Periodos.Values.All(p => p != ResultadoPeriodo.Libre);
}

/// <summary>Aplica las reglas de carga sin conocer Excel, red ni persistencia.</summary>
public sealed class ProcesadorLote(IReglasCarga reglas)
{
    public async Task<ResultadoProceso> ProcesarAsync(int idCarga, IEnumerable<FilaCruda> filas, CancellationToken ct = default)
    {
        var candidatas = new List<FilaProducto>();
        var rechazadas = new List<FilaRechazada>();
        var observaciones = new List<FilaRechazada>();

        foreach (var cruda in filas)
        {
            var r = NormalizadorFila.Normalizar(cruda);
            if (r.Descartada) { rechazadas.AddRange(r.Observaciones); continue; }
            candidatas.Add(r.Fila!);
            observaciones.AddRange(r.Observaciones);
        }

        // Un archivo puede mezclar períodos; cada uno se consulta una sola vez.
        var periodos = new Dictionary<string, ResultadoPeriodo>();
        foreach (var periodo in candidatas.Select(c => c.Periodo).Distinct().Order())
            periodos[periodo] = await reglas.ResolverPeriodoAsync(idCarga, periodo, ct);

        // Los períodos no disponibles se rechazan sin bloquear los períodos libres.
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

        // Ante duplicados del archivo, la primera fila conserva la clave.
        var vistas = new HashSet<ClaveProducto>();
        var unicas = new List<FilaProducto>(vivas.Count);
        foreach (var fila in vivas)
        {
            var clave = new ClaveProducto(fila.Periodo, fila.CodigoProducto);
            if (vistas.Add(clave)) { unicas.Add(fila); continue; }

            rechazadas.Add(new FilaRechazada(fila.NumeroFila, fila.Periodo, fila.CodigoProducto,
                nameof(fila.CodigoProducto), MotivoRechazo.Existente, fila.CodigoProducto));
        }

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

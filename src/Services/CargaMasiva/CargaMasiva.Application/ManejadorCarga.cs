using BuildingBlocks;
using CargaMasiva.Domain;
using Microsoft.Extensions.Logging;

namespace CargaMasiva.Application;

/// <summary>Puerto sobre <c>ExcelDataReader</c> (§C-DIP): Application no conoce la librería concreta.</summary>
public interface ILectorExcel
{
    IEnumerable<FilaCruda> Leer(Stream stream);
}

/// <summary>Puerto sobre el insert set-based a Postgres (§C-DIP): Application no conoce Npgsql.</summary>
public interface IInsertadorMasivo
{
    Task<int> InsertarAsync(int idCarga, IReadOnlyList<FilaProducto> filas, CancellationToken ct);
}

/// <summary>
/// El caso de uso del §3️⃣: descargar, leer, validar, insertar, auditar y avisar.
/// Vive en Application (no en Infrastructure, donde estaba antes) porque ES el caso
/// de uso — orquesta sobre puertos (<see cref="IAlmacenCarga"/>, <see cref="ILectorExcel"/>,
/// <see cref="IInsertadorMasivo"/>, <see cref="IPublicadorNotificacion"/>, <see cref="IRepositorioCargas"/>),
/// nunca sobre la librería concreta detrás de cada uno (design.md §D1 de
/// arquitectura-hexagonal-transversal). <see cref="ProcesadorLote"/> se inyecta, no se
/// instancia acá adentro — instanciar un colaborador a mano es la misma violación de
/// DIP que depender del tipo concreto.
/// </summary>
public sealed class ManejadorCarga(
    IRepositorioCargas repositorio,
    IAlmacenCarga almacen,
    ILectorExcel lector,
    ProcesadorLote procesadorLote,
    IInsertadorMasivo insertador,
    IPublicadorNotificacion publicador,
    ILogger<ManejadorCarga> log)
{
    /// <summary>Procesa el mensaje y devuelve el estado alcanzado por la carga.</summary>
    public async Task<Resultado<EstadoCarga>> ProcesarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct)
    {
        var carga = await repositorio.ObtenerAsync(mensaje.IdCarga, ct);

        // Una reentrega terminal no debe reabrir ni duplicar una carga resuelta.
        if (carga.Estado is not (EstadoCarga.Pendiente or EstadoCarga.EnProceso))
        {
            log.LogWarning("Carga {IdCarga} reentregada en estado {Estado}; se ignora.", carga.Id, carga.Estado);
            return Resultado<EstadoCarga>.Exito(carga.Estado);
        }

        if (carga.Estado == EstadoCarga.Pendiente)
        {
            carga.Transicionar(EstadoCarga.EnProceso);
            await repositorio.GuardarCambiosAsync(ct);
        }

        await using var contenido = await almacen.DescargarAsync(mensaje.RutaArchivo, ct);

        // Las reglas cruzadas requieren el lote completo; este punto hace explícito
        // el coste O(n) de memoria. Convertirlo a ventanas exige rediseñar reglas y auditoría.
        var filas = lector.Leer(contenido).ToList();

        var resultadoLote = await procesadorLote.ProcesarAsync(carga.Id, filas, ct);
        var insertadas = await insertador.InsertarAsync(carga.Id, resultadoLote.Aceptadas, ct);

        // Se registra cada rechazo y observación para conservar trazabilidad. Por eso
        // pedir un total exacto de errores cuesta O(n), aunque el detalle se pagine.
        repositorio.AgregarErrores(resultadoLote.Rechazadas.Concat(resultadoLote.Observaciones).Select(r =>
            new DetalleCargaError
            {
                CargaArchivoId = carga.Id,
                NumeroFila = r.NumeroFila,
                Periodo = r.Periodo,
                CodigoProducto = r.CodigoProducto,
                Columna = r.Columna,
                Motivo = r.Motivo,
                ValorCrudo = r.ValorCrudo,
                FechaRegistro = DateTimeOffset.UtcNow
            }));

        // El procedimiento reserva el período; el conteo sólo se conoce tras insertar.
        var porPeriodo = resultadoLote.Aceptadas.GroupBy(f => f.Periodo).ToDictionary(g => g.Key, g => g.Count());
        if (porPeriodo.Count > 0)
        {
            var periodos = await repositorio.ObtenerPeriodosAsync(carga.Id, ct);
            foreach (var p in periodos.Where(p => porPeriodo.ContainsKey(p.Periodo)))
                p.FilasInsertadas = porPeriodo[p.Periodo];
        }

        carga.TotalFilas = resultadoLote.TotalFilas;
        carga.FilasInsertadas = insertadas;
        // El procedimiento es la fuente del conteo: puede absorber conflictos que
        // aparezcan entre la consulta preventiva y el INSERT.
        carga.FilasRechazadas = resultadoLote.TotalFilas - insertadas;

        if (resultadoLote.NingunPeriodoAceptado)
        {
            // Bloqueada prevalece: comunica que existe una carga activa que puede reintentarse.
            carga.Transicionar(resultadoLote.Periodos.Values.Any(v => v == ResultadoPeriodo.Bloqueado)
                ? EstadoCarga.Bloqueada
                : EstadoCarga.Rechazada);
            await repositorio.GuardarCambiosAsync(ct);
            // No hay un resultado de carga que deba notificarse.
            return Resultado<EstadoCarga>.Exito(carga.Estado);
        }

        carga.Transicionar(EstadoCarga.Cargado);
        carga.Transicionar(EstadoCarga.Finalizado);
        await repositorio.GuardarCambiosAsync(ct);

        // Una falla esperada de notificación no revierte una carga ya finalizada;
        // una excepción inesperada del adaptador sigue propagándose.
        var resultadoPublicacion = await publicador.PublicarAsync(
            new MensajeNotificacion(carga.Id, carga.Usuario, carga.FechaFin!.Value), correlationId, ct);

        if (!resultadoPublicacion.EsExitoso)
            log.LogWarning("No se pudo publicar la notificación de la carga {IdCarga}: {Error}", carga.Id, resultadoPublicacion.Error);

        return Resultado<EstadoCarga>.Exito(carga.Estado);
    }
}

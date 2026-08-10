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
    /// <summary>
    /// Retorna el estado terminal alcanzado (design.md §D4 de
    /// arquitectura-hexagonal-transversal) — comunica el resultado sin que el
    /// llamador (o un test) tenga que releer la base para saber qué pasó.
    /// </summary>
    public async Task<Resultado<EstadoCarga>> ProcesarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct)
    {
        var carga = await repositorio.ObtenerAsync(mensaje.IdCarga, ct);

        // §C8 — reentrega de un mensaje ya procesado (o de una carga ya resuelta
        // por otro motivo). Reprocesar violaría la máquina de estados y podría
        // duplicar trabajo; se ignora en silencio, no es un error.
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

        // ILectorExcel avanza hacia delante, pero este caso de uso materializa todas
        // las filas porque ProcesadorLote recibe el lote completo para resolver
        // períodos, duplicados y rechazos de forma consistente. Por tanto el
        // pipeline actual es O(n) en memoria: "lector streaming" no significa
        // "procesamiento streaming". La prueba real de 2M filas está documentada
        // en docs/pruebas-de-escala.md; convertirlo a ventanas requeriría redefinir
        // la coordinación de períodos y la auditoría, no reemplazar solo este ToList.
        var filas = lector.Leer(contenido).ToList();

        var resultadoLote = await procesadorLote.ProcesarAsync(carga.Id, filas, ct);
        var insertadas = await insertador.InsertarAsync(carga.Id, resultadoLote.Aceptadas, ct);

        // Auditoría (§3.3c): rechazos de negocio + los ajustes de "valor por
        // defecto aplicado" (§2.4b) — no son un rechazo, pero el usuario debe
        // poder verlos igual que cualquier otro motivo. Se persiste una fila por
        // hallazgo: una carga de 2M rechazada genera 2M auditorías. Eso preserva
        // trazabilidad, pero hace que pedir el total exacto de errores sea O(n);
        // ConsultaCargasEf limita el payload, no el CountAsync que calcula ese total.
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

        // sp_resolver_periodo inserta carga_periodo con filas_insertadas=0 (no
        // sabe cuántas van a entrar todavía); acá ya se sabe, así que se completa
        // — el detalle por periodo (§5️⃣) lo expone tal cual.
        var porPeriodo = resultadoLote.Aceptadas.GroupBy(f => f.Periodo).ToDictionary(g => g.Key, g => g.Count());
        if (porPeriodo.Count > 0)
        {
            var periodos = await repositorio.ObtenerPeriodosAsync(carga.Id, ct);
            foreach (var p in periodos.Where(p => porPeriodo.ContainsKey(p.Periodo)))
                p.FilasInsertadas = porPeriodo[p.Periodo];
        }

        carga.TotalFilas = resultadoLote.TotalFilas;
        carga.FilasInsertadas = insertadas;
        // La cuenta real del motor (no resultadoLote.Aceptadas.Count): si otra carga
        // concurrente insertó el mismo (periodo, código) justo entre el chequeo de
        // ObtenerExistentesAsync y este INSERT, el ON CONFLICT del SP la absorbe
        // sin lanzar, pero no queda en DetalleCargaError — ventana de carrera
        // aceptada, de probabilidad casi nula en el alcance de este reto.
        carga.FilasRechazadas = resultadoLote.TotalFilas - insertadas;

        if (resultadoLote.NingunPeriodoAceptado)
        {
            // maquina-estados.md: sin trabajo útil, la carga termina Rechazada o
            // Bloqueada. Bloqueada gana si ALGÚN periodo está bloqueado por una
            // carga activa — es la lectura más accionable ("hay una en curso,
            // reintentar luego") frente a "ya está cargado, no hay nada que hacer".
            carga.Transicionar(resultadoLote.Periodos.Values.Any(v => v == ResultadoPeriodo.Bloqueado)
                ? EstadoCarga.Bloqueada
                : EstadoCarga.Rechazada);
            await repositorio.GuardarCambiosAsync(ct);
            return Resultado<EstadoCarga>.Exito(carga.Estado);   // Terminal sin Notificado (maquina-estados.md): no hay notificación que publicar.
        }

        carga.Transicionar(EstadoCarga.Cargado);
        carga.Transicionar(EstadoCarga.Finalizado);
        await repositorio.GuardarCambiosAsync(ct);

        // Resultado, no catch (design.md §D4): la carga ya es Finalizado en este
        // punto — un fallo al publicar la notificación es un problema operativo
        // secundario, no una razón para tratar como fallido el procesamiento que
        // ya tuvo éxito. Se audita con LogWarning; un bug real dentro de
        // IPublicador (no un Resultado.Fallo) sigue propagándose como excepción.
        var resultadoPublicacion = await publicador.PublicarAsync(
            new MensajeNotificacion(carga.Id, carga.Usuario, carga.FechaFin!.Value), correlationId, ct);

        if (!resultadoPublicacion.EsExitoso)
            log.LogWarning("No se pudo publicar la notificación de la carga {IdCarga}: {Error}", carga.Id, resultadoPublicacion.Error);

        return Resultado<EstadoCarga>.Exito(carga.Estado);
    }
}

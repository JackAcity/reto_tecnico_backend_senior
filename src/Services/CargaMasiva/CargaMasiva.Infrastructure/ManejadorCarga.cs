using Almacenamiento;
using CargaMasiva.Application;
using CargaMasiva.Domain;
using Mensajeria;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistencia;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// El caso de uso del §3️⃣: descargar, leer, validar, insertar, auditar y avisar.
/// Coordina infraestructura (DB, SeaweedFS, RabbitMQ) alrededor del núcleo puro
/// (<see cref="ProcesadorLote"/>), que no sabe que ninguna de las tres existe.
/// </summary>
public sealed class ManejadorCarga(
    RetoDbContext db,
    IAlmacenArchivos almacen,
    IReglasCarga reglas,
    InsertadorMasivo insertador,
    IPublicador publicador,
    ILogger<ManejadorCarga> log)
{
    public async Task ProcesarAsync(MensajeCarga mensaje, string correlationId, CancellationToken ct)
    {
        var carga = await db.CargaArchivos.SingleAsync(c => c.Id == mensaje.IdCarga, ct);

        // §C8 — reentrega de un mensaje ya procesado (o de una carga ya resuelta
        // por otro motivo). Reprocesar violaría la máquina de estados y podría
        // duplicar trabajo; se ignora en silencio, no es un error.
        if (carga.Estado is not (EstadoCarga.Pendiente or EstadoCarga.EnProceso))
        {
            log.LogWarning("Carga {IdCarga} reentregada en estado {Estado}; se ignora.", carga.Id, carga.Estado);
            return;
        }

        if (carga.Estado == EstadoCarga.Pendiente)
        {
            carga.Transicionar(EstadoCarga.EnProceso);
            await db.SaveChangesAsync(ct);
        }

        await using var contenido = await almacen.DescargarAsync(mensaje.RutaArchivo, ct);
        var filas = new LectorExcel().Leer(contenido).ToList();

        var resultado = await new ProcesadorLote(reglas).ProcesarAsync(carga.Id, filas, ct);
        var insertadas = await insertador.InsertarAsync(carga.Id, resultado.Aceptadas, ct);

        // Auditoría (§3.3c): rechazos de negocio + los ajustes de "valor por
        // defecto aplicado" (§2.4b) — no son un rechazo, pero el usuario debe
        // poder verlos igual que cualquier otro motivo.
        db.DetalleCargaErrores.AddRange(resultado.Rechazadas.Concat(resultado.Observaciones).Select(r =>
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
        var porPeriodo = resultado.Aceptadas.GroupBy(f => f.Periodo).ToDictionary(g => g.Key, g => g.Count());
        if (porPeriodo.Count > 0)
        {
            var periodos = await db.CargaPeriodos.Where(p => p.CargaArchivoId == carga.Id).ToListAsync(ct);
            foreach (var p in periodos.Where(p => porPeriodo.ContainsKey(p.Periodo)))
                p.FilasInsertadas = porPeriodo[p.Periodo];
        }

        carga.TotalFilas = resultado.TotalFilas;
        carga.FilasInsertadas = insertadas;
        // La cuenta real del motor (no resultado.Aceptadas.Count): si otra carga
        // concurrente insertó el mismo (periodo, código) justo entre el chequeo de
        // ObtenerExistentesAsync y este INSERT, el ON CONFLICT del SP la absorbe
        // sin lanzar, pero no queda en DetalleCargaError — ventana de carrera
        // aceptada, de probabilidad casi nula en el alcance de este reto.
        carga.FilasRechazadas = resultado.TotalFilas - insertadas;

        if (resultado.NingunPeriodoAceptado)
        {
            // maquina-estados.md: sin trabajo útil, la carga termina Rechazada o
            // Bloqueada. Bloqueada gana si ALGÚN periodo está bloqueado por una
            // carga activa — es la lectura más accionable ("hay una en curso,
            // reintentar luego") frente a "ya está cargado, no hay nada que hacer".
            carga.Transicionar(resultado.Periodos.Values.Any(v => v == ResultadoPeriodo.Bloqueado)
                ? EstadoCarga.Bloqueada
                : EstadoCarga.Rechazada);
            await db.SaveChangesAsync(ct);
            return;   // Terminal sin Notificado (maquina-estados.md): no hay notificación que publicar.
        }

        carga.Transicionar(EstadoCarga.Cargado);
        carga.Transicionar(EstadoCarga.Finalizado);
        await db.SaveChangesAsync(ct);

        await publicador.PublicarAsync(
            Topologia.RkNotificacion,
            new MensajeNotificacion(carga.Id, carga.Usuario, carga.FechaFin!.Value),
            correlationId, ct);
    }
}

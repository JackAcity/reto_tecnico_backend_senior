using CargaMasiva.Domain;
using Mensajeria;
using Microsoft.EntityFrameworkCore;
using Persistencia;

namespace Notificaciones.Api;

/// <summary>
/// El caso de uso del §4️⃣: leer la notificación, mandar el correo, marcar
/// Notificado. Los números del resumen (insertados/rechazados) no viajan en el
/// mensaje —viven en carga_archivo, la única fuente de verdad— así que se leen
/// de ahí, igual que el estado.
/// </summary>
public sealed class ManejadorNotificacion(RetoDbContext db, IEnviadorCorreo correo, ILogger<ManejadorNotificacion> log)
{
    public async Task ProcesarAsync(MensajeNotificacion mensaje, CancellationToken ct)
    {
        var carga = await db.CargaArchivos.SingleAsync(c => c.Id == mensaje.IdCarga, ct);

        // §C8 — reentrega de un mensaje ya notificado: no hay que reenviar el
        // correo. Si por algún motivo la carga no llegó a Finalizado (no debería
        // pasar: Notificaciones solo recibe mensajes que CargaMasiva publicó
        // después de transicionar), tampoco hay nada que hacer todavía.
        if (carga.Estado != EstadoCarga.Finalizado)
        {
            log.LogWarning("Notificación de carga {IdCarga} ignorada: estado actual {Estado}.", carga.Id, carga.Estado);
            return;
        }

        await correo.EnviarResumenCargaAsync(
            carga.Usuario, carga.Id, carga.FilasInsertadas, carga.FilasRechazadas, mensaje.FechaFin, ct);

        carga.Transicionar(EstadoCarga.Notificado);
        await db.SaveChangesAsync(ct);
    }
}

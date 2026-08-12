using BuildingBlocks;
using Microsoft.Extensions.Logging;

namespace Notificaciones.Api;

/// <summary>
/// Puerto de acceso a datos de <see cref="ManejadorNotificacion"/> (design.md
/// §D2 de arquitectura-hexagonal-transversal): Application no conoce EF Core.
/// </summary>
public interface IRepositorioNotificaciones
{
    Task<CargaPorNotificar> ObtenerAsync(int idCarga, CancellationToken ct);
    Task GuardarCambiosAsync(CancellationToken ct);
}

/// <summary>
/// El caso de uso del §4️⃣: leer la notificación, mandar el correo, marcar
/// Notificado. Los números del resumen (insertados/rechazados) no viajan en el
/// mensaje —viven en carga_archivo, la única fuente de verdad— así que se leen
/// de ahí, igual que el estado.
/// </summary>
public sealed class ManejadorNotificacion(IRepositorioNotificaciones repositorio, IEnviadorCorreo correo, ILogger<ManejadorNotificacion> log)
{
    public async Task ProcesarAsync(MensajeNotificacion mensaje, CancellationToken ct)
    {
        var carga = await repositorio.ObtenerAsync(mensaje.IdCarga, ct);

        // §C8 — reentrega de un mensaje ya notificado: no hay que reenviar el
        // correo. Si por algún motivo la carga no llegó a Finalizado (no debería
        // pasar: Notificaciones solo recibe mensajes que CargaMasiva publicó
        // después de transicionar), tampoco hay nada que hacer todavía.
        if (!carga.EstaListaParaNotificar)
        {
            log.LogWarning("Notificación de carga {IdCarga} ignorada: estado actual {Estado}.", carga.Id, carga.Estado);
            return;
        }

        await correo.EnviarResumenCargaAsync(
            carga.Usuario, carga.Id, carga.FilasInsertadas, carga.FilasRechazadas, mensaje.FechaFin, ct);

        carga.MarcarNotificada();
        await repositorio.GuardarCambiosAsync(ct);
    }
}

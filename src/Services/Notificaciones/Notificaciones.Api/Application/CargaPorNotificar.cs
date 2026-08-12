namespace Notificaciones.Api;

/// <summary>
/// Vista de la carga que Notificaciones necesita para entregar un correo. Es un
/// modelo local: la tabla física común no expone el agregado de CargaMasiva.
/// </summary>
public sealed class CargaPorNotificar
{
    public int Id { get; set; }
    public string Usuario { get; set; } = "";
    public int FilasInsertadas { get; set; }
    public int FilasRechazadas { get; set; }
    public EstadoNotificacionCarga Estado { get; set; }

    public bool EstaListaParaNotificar => Estado == EstadoNotificacionCarga.Finalizado;

    public void MarcarNotificada() => Estado = EstadoNotificacionCarga.Notificado;
}

public enum EstadoNotificacionCarga { Pendiente, EnProceso, Cargado, Finalizado, Notificado, Rechazada, Bloqueada, Fallida }
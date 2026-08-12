namespace BuildingBlocks;

/// <summary>
/// Nombres del contrato asíncrono de cargas. No dependen de RabbitMQ ni de un
/// adaptador: productores y consumidores los comparten para conservar la
/// compatibilidad del protocolo al evolucionar sus implementaciones locales.
/// </summary>
public static class TopologiaMensajeria
{
    public const string Exchange = "cargas";
    public const string ExchangeReintento = "cargas.reintento";

    public const string RkCarga = "carga.masiva";
    public const string RkNotificacion = "carga.notificacion";
    public const string RkCargaMuerto = "carga.masiva.muerto";
    public const string RkNotificacionMuerto = "carga.notificacion.muerto";

    public const string ColaCarga = "carga_masiva";
    public const string ColaNotificaciones = "notificaciones";
    public const string ColaCargaReintento = "carga_masiva.reintento";
    public const string ColaCargaMuertos = "carga_masiva.muertos";
    public const string ColaNotificacionesReintento = "notificaciones.reintento";
    public const string ColaNotificacionesMuertos = "notificaciones.muertos";
}
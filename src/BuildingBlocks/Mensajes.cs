namespace BuildingBlocks;

/// <summary>
/// Contratos de integración independientes del transporte. El correlationId viaja
/// como metadata del adaptador, no como parte del cuerpo del mensaje.
/// </summary>
public sealed record MensajeCarga(int IdCarga, string RutaArchivo, string Usuario);

public sealed record MensajeNotificacion(int IdCarga, string Usuario, DateTimeOffset FechaFin);
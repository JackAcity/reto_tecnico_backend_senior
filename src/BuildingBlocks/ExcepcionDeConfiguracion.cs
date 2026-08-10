namespace BuildingBlocks;

/// <summary>
/// Configuración requerida faltante (variable de entorno / appsettings) en un
/// punto alcanzable durante un request HTTP en curso (design.md de
/// clasificacion-excepciones-config) — no un error del cliente. Sin esta marca,
/// <see cref="GlobalExceptionHandler"/> no puede distinguirla de una
/// <see cref="InvalidOperationException"/> de negocio (ej. <c>TransicionInvalidaException</c>),
/// que sí expone su mensaje con 400.
/// </summary>
public sealed class ExcepcionDeConfiguracion(string mensaje) : InvalidOperationException(mensaje);

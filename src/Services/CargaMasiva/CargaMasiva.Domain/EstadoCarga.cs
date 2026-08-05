namespace CargaMasiva.Domain;

/// <summary>
/// Estados de una carga. El enunciado enumera cinco, pero exige comportamientos
/// ("rechazada", "bloqueada", "fallidos") que no tenían estado asignado.
/// Ver specs/maquina-estados.md.
/// </summary>
public enum EstadoCarga
{
    Pendiente,
    EnProceso,
    Cargado,
    Finalizado,
    Notificado,
    Rechazada,
    Bloqueada,
    Fallida
}

public static class MaquinaEstados
{
    private static readonly Dictionary<EstadoCarga, EstadoCarga[]> Permitidas = new()
    {
        [EstadoCarga.Pendiente] = [EstadoCarga.EnProceso, EstadoCarga.Fallida],
        [EstadoCarga.EnProceso] = [EstadoCarga.Cargado, EstadoCarga.Rechazada, EstadoCarga.Bloqueada, EstadoCarga.Fallida],
        [EstadoCarga.Cargado] = [EstadoCarga.Finalizado],
        [EstadoCarga.Finalizado] = [EstadoCarga.Notificado],
        [EstadoCarga.Notificado] = [],
        [EstadoCarga.Rechazada] = [],
        [EstadoCarga.Bloqueada] = [],
        [EstadoCarga.Fallida] = []
    };

    public static bool EsTransicionValida(EstadoCarga desde, EstadoCarga hacia) =>
        Permitidas[desde].Contains(hacia);

    /// <summary>Lanza si la transición no está permitida. Evita estados imposibles por bug de orquestación.</summary>
    public static void Validar(EstadoCarga desde, EstadoCarga hacia)
    {
        if (!EsTransicionValida(desde, hacia))
            throw new TransicionInvalidaException(desde, hacia);
    }

    public static bool EsTerminal(EstadoCarga estado) => Permitidas[estado].Length == 0;
}

public sealed class TransicionInvalidaException(EstadoCarga desde, EstadoCarga hacia)
    : InvalidOperationException($"Transición inválida: {desde} → {hacia}")
{
    public EstadoCarga Desde { get; } = desde;
    public EstadoCarga Hacia { get; } = hacia;
}

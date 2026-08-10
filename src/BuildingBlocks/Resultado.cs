namespace BuildingBlocks;

/// <summary>
/// Resultado de un caso de uso con valor de éxito. Para fallos de negocio
/// esperados (design.md §D3/§D4 de arquitectura-hexagonal-transversal) — no
/// reemplaza excepciones para lo verdaderamente excepcional.
/// </summary>
public sealed class Resultado<T>
{
    private readonly T? valor;

    public bool EsExitoso { get; }
    public T Valor => EsExitoso
        ? valor!
        : throw new InvalidOperationException("Un resultado fallido no contiene valor.");
    public string? Error { get; }

    private Resultado(bool esExitoso, T? valor, string? error)
    {
        EsExitoso = esExitoso;
        this.valor = valor;
        Error = error;
    }

    public static Resultado<T> Exito(T valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return new(true, valor, null);
    }

    public static Resultado<T> Fallo(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(false, default, error);
    }
}

/// <summary>Variante de <see cref="Resultado{T}"/> para casos de uso sin valor de retorno útil.</summary>
public sealed class Resultado
{
    public bool EsExitoso { get; }
    public string? Error { get; }

    private Resultado(bool esExitoso, string? error)
    {
        EsExitoso = esExitoso;
        Error = error;
    }

    public static Resultado Exito() => new(true, null);

    public static Resultado Fallo(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(false, error);
    }
}

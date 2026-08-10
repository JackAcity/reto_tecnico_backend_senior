namespace BuildingBlocks;

/// <summary>
/// Resultado de un caso de uso con valor de éxito. Para fallos de negocio
/// esperados (design.md §D3/§D4 de arquitectura-hexagonal-transversal) — no
/// reemplaza excepciones para lo verdaderamente excepcional.
/// </summary>
public readonly struct Resultado<T>
{
    public bool EsExitoso { get; }
    public T? Valor { get; }
    public string? Error { get; }

    private Resultado(bool esExitoso, T? valor, string? error)
    {
        EsExitoso = esExitoso;
        Valor = valor;
        Error = error;
    }

    public static Resultado<T> Exito(T valor) => new(true, valor, null);
    public static Resultado<T> Fallo(string error) => new(false, default, error);
}

/// <summary>Variante de <see cref="Resultado{T}"/> para casos de uso sin valor de retorno útil.</summary>
public readonly struct Resultado
{
    public bool EsExitoso { get; }
    public string? Error { get; }

    private Resultado(bool esExitoso, string? error)
    {
        EsExitoso = esExitoso;
        Error = error;
    }

    public static Resultado Exito() => new(true, null);
    public static Resultado Fallo(string error) => new(false, error);
}

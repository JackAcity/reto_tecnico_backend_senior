using System.Globalization;

namespace CargaMasiva.Domain;

/// <summary>Motivo por el que una fila se descartó o se ajustó. Se persiste en detalle_carga_error (§3.3c).</summary>
public enum MotivoRechazo
{
    PeriodoYaCargado,
    PeriodoBloqueado,
    Existente,
    ValorPorDefectoAplicado,
    PrecioInvalido,
    PeriodoRequerido,
    PeriodoFormatoInvalido,
    CodigoRequerido
}

/// <summary>Fila cruda del Excel, tal como sale del lector. Sin interpretar.</summary>
public sealed record FilaCruda(int NumeroFila, string? Periodo, string? CodigoProducto, string? NombreProducto, string? Precio)
{
    public bool EstaVacia =>
        string.IsNullOrWhiteSpace(Periodo) &&
        string.IsNullOrWhiteSpace(CodigoProducto) &&
        string.IsNullOrWhiteSpace(NombreProducto) &&
        string.IsNullOrWhiteSpace(Precio);
}

/// <summary>Fila ya validada y normalizada, lista para insertar.</summary>
public sealed record FilaProducto(int NumeroFila, string Periodo, string CodigoProducto, string NombreProducto, decimal Precio);

/// <summary>Una fila descartada, con el detalle que necesita el usuario para corregirla.</summary>
public sealed record FilaRechazada(int NumeroFila, string? Periodo, string? CodigoProducto, string? Columna, MotivoRechazo Motivo, string? ValorCrudo);

public sealed record ResultadoNormalizacion(FilaProducto? Fila, IReadOnlyList<FilaRechazada> Observaciones, bool Descartada);

/// <summary>
/// Reglas de limpieza del §3.3e/f: columna vacía → valor por defecto,
/// fila vacía → no se registra. Lógica pura: sin base de datos, sin Excel, sin red.
/// </summary>
public static class NormalizadorFila
{
    public const string NombrePorDefecto = "SIN NOMBRE";
    public const decimal PrecioPorDefecto = 0m;

    public static ResultadoNormalizacion Normalizar(FilaCruda cruda)
    {
        // "Si hay filas vacías en el archivo no se deben registrar" — se descartan
        // en silencio: no son un error del usuario, son relleno del archivo.
        if (cruda.EstaVacia)
            return new ResultadoNormalizacion(null, [], Descartada: true);

        var observaciones = new List<FilaRechazada>();

        var periodo = cruda.Periodo?.Trim();
        var codigo = cruda.CodigoProducto?.Trim();

        if (string.IsNullOrWhiteSpace(periodo))
            return Descartar(cruda, nameof(cruda.Periodo), MotivoRechazo.PeriodoRequerido, cruda.Periodo);

        if (!EsPeriodoValido(periodo))
            return Descartar(cruda, nameof(cruda.Periodo), MotivoRechazo.PeriodoFormatoInvalido, cruda.Periodo);

        if (string.IsNullOrWhiteSpace(codigo))
            return Descartar(cruda, nameof(cruda.CodigoProducto), MotivoRechazo.CodigoRequerido, cruda.CodigoProducto);

        // Columna vacía → valor por defecto. La fila SÍ se inserta; el ajuste se audita.
        var nombre = cruda.NombreProducto?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = NombrePorDefecto;
            observaciones.Add(new FilaRechazada(cruda.NumeroFila, periodo, codigo,
                nameof(cruda.NombreProducto), MotivoRechazo.ValorPorDefectoAplicado, cruda.NombreProducto));
        }

        decimal precio;
        if (string.IsNullOrWhiteSpace(cruda.Precio))
        {
            precio = PrecioPorDefecto;
            observaciones.Add(new FilaRechazada(cruda.NumeroFila, periodo, codigo,
                nameof(cruda.Precio), MotivoRechazo.ValorPorDefectoAplicado, cruda.Precio));
        }
        else if (!decimal.TryParse(cruda.Precio.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out precio))
        {
            // No numérico se trata como vacío: el enunciado pide valor por defecto ante
            // ausencia de dato, y un texto en columna numérica es ausencia de dato válido.
            precio = PrecioPorDefecto;
            observaciones.Add(new FilaRechazada(cruda.NumeroFila, periodo, codigo,
                nameof(cruda.Precio), MotivoRechazo.ValorPorDefectoAplicado, cruda.Precio));
        }
        else if (precio < 0)
        {
            // Un precio negativo sí es un dato presente y erróneo: se descarta la fila.
            return Descartar(cruda, nameof(cruda.Precio), MotivoRechazo.PrecioInvalido, cruda.Precio);
        }

        return new ResultadoNormalizacion(
            new FilaProducto(cruda.NumeroFila, periodo, codigo, nombre, precio),
            observaciones,
            Descartada: false);
    }

    // yyyy-MM. El archivo de muestra usa "2025-01", "2025-02", "2025-03".
    public static bool EsPeriodoValido(string periodo) =>
        DateTime.TryParseExact(periodo, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static ResultadoNormalizacion Descartar(FilaCruda cruda, string columna, MotivoRechazo motivo, string? valor) =>
        new(null, [new FilaRechazada(cruda.NumeroFila, cruda.Periodo?.Trim(), cruda.CodigoProducto?.Trim(), columna, motivo, valor)], Descartada: true);
}

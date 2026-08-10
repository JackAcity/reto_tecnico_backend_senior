using System.Text;
using CargaMasiva.Application;
using CargaMasiva.Domain;
using ExcelDataReader;

namespace CargaMasiva.Infrastructure;

/// <summary>
/// Lectura forward-only del .xlsx. ExcelDataReader mantiene memoria constante
/// sin importar el tamaño del archivo: el reto se llama "carga masiva", así que
/// cargar el libro entero en memoria (ClosedXML/EPPlus) sería la decisión incorrecta.
/// EPPlus además no es libre para uso comercial. Implementa <see cref="ILectorExcel"/>
/// (Application) — el caso de uso depende del puerto, no de ExcelDataReader.
/// </summary>
public sealed class LectorExcel : ILectorExcel
{
    static LectorExcel() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// Devuelve las filas de datos (la primera fila del archivo es el encabezado).
    /// <paramref name="stream"/> se consume de forma perezosa.
    /// </summary>
    public IEnumerable<FilaCruda> Leer(Stream stream)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);

        if (!reader.Read()) yield break;   // encabezado: Periodo | CodigoProducto | NombreProducto | Precio

        var numeroFila = 1;
        while (reader.Read())
        {
            numeroFila++;
            yield return new FilaCruda(
                NumeroFila: numeroFila,
                Periodo: Celda(reader, 0),
                CodigoProducto: Celda(reader, 1),
                NombreProducto: Celda(reader, 2),
                Precio: Celda(reader, 3));
        }
    }

    private static string? Celda(IExcelDataReader reader, int i)
    {
        if (i >= reader.FieldCount || reader.IsDBNull(i)) return null;

        return reader.GetValue(i) switch
        {
            null => null,
            // Excel guarda los números como double; ToString invariante evita que la
            // configuración regional del contenedor convierta 418.27 en "418,27".
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM"),
            var v => v.ToString()
        };
    }
}

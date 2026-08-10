namespace CargaMasiva.Application;

/// <summary>Puerto de lectura del archivo asociado a una carga.</summary>
public interface IAlmacenCarga
{
    Task<Stream> DescargarAsync(string ruta, CancellationToken ct);
}

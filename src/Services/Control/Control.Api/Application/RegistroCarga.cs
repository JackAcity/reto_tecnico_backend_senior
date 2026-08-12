namespace Control.Api;

/// <summary>
/// Registro que Control posee para el ciclo de vida visible de una carga. Comparte
/// una tabla física con procesamiento por requisito del reto, no un tipo de dominio.
/// </summary>
public sealed class RegistroCarga
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string? RutaArchivo { get; set; }
    public long TamanoBytes { get; set; }
    public string Usuario { get; set; } = "";
    public DateTimeOffset FechaRegistro { get; set; }
    public EstadoRegistroCarga Estado { get; set; } = EstadoRegistroCarga.Pendiente;
    public DateTimeOffset? FechaFin { get; set; }
    public int TotalFilas { get; set; }
    public int FilasInsertadas { get; set; }
    public int FilasRechazadas { get; set; }
    public string? MensajeError { get; set; }
    public string CorrelationId { get; set; } = "";
    public List<PeriodoRegistroCarga> Periodos { get; set; } = [];

    public void MarcarFallida(string mensaje)
    {
        Estado = EstadoRegistroCarga.Fallida;
        MensajeError = mensaje;
        FechaFin ??= DateTimeOffset.UtcNow;
    }
}

public enum EstadoRegistroCarga { Pendiente, EnProceso, Cargado, Finalizado, Notificado, Rechazada, Bloqueada, Fallida }

public sealed class PeriodoRegistroCarga
{
    public int Id { get; set; }
    public int CargaArchivoId { get; set; }
    public RegistroCarga? CargaArchivo { get; set; }
    public string Periodo { get; set; } = "";
    public string Estado { get; set; } = "";
    public int FilasInsertadas { get; set; }
}

public sealed class ErrorRegistroCarga
{
    public int Id { get; set; }
    public int CargaArchivoId { get; set; }
    public int NumeroFila { get; set; }
    public string? Periodo { get; set; }
    public string? CodigoProducto { get; set; }
    public string? Columna { get; set; }
    public string Motivo { get; set; } = "";
    public string? ValorCrudo { get; set; }
}
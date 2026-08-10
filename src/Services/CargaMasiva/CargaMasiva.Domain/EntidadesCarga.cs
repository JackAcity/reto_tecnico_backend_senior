namespace CargaMasiva.Domain;

/// <summary>Una subida de archivo. Su Id es el <c>idCarga</c> del contrato de mensaje.</summary>
public sealed class CargaArchivo
{
    public int Id { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string? RutaArchivo { get; set; }
    public long TamanoBytes { get; set; }
    public string Usuario { get; set; } = "";
    public DateTimeOffset FechaRegistro { get; set; }
    public EstadoCarga Estado { get; set; } = EstadoCarga.Pendiente;
    public DateTimeOffset? FechaFin { get; set; }
    public int TotalFilas { get; set; }
    public int FilasInsertadas { get; set; }
    public int FilasRechazadas { get; set; }
    public string? MensajeError { get; set; }
    public string CorrelationId { get; set; } = "";

    public List<CargaPeriodo> Periodos { get; set; } = [];

    public void Transicionar(EstadoCarga hacia)
    {
        MaquinaEstados.Validar(Estado, hacia);
        Estado = hacia;
        if (MaquinaEstados.EsTerminal(hacia) || hacia == EstadoCarga.Cargado)
            FechaFin ??= DateTimeOffset.UtcNow;
    }
}

/// <summary>Resultado de resolver un periodo del archivo.</summary>
public sealed class CargaPeriodo
{
    public int Id { get; set; }
    public int CargaArchivoId { get; set; }
    public CargaArchivo? CargaArchivo { get; set; }
    public string Periodo { get; set; } = "";
    public string Estado { get; set; } = EstadoPeriodo.Aceptado;
    public int FilasInsertadas { get; set; }
}

public static class EstadoPeriodo
{
    public const string Aceptado = "Aceptado";
    public const string YaCargado = "YaCargado";
    public const string Bloqueado = "Bloqueado";
}

/// <summary>Registro del Excel persistido con su clave de negocio.</summary>
public sealed class DataProcesada
{
    public int Id { get; set; }
    public string Periodo { get; set; } = "";
    public string CodigoProducto { get; set; } = "";
    public string NombreProducto { get; set; } = "";
    public decimal Precio { get; set; }
    public int CargaArchivoId { get; set; }
    public CargaArchivo? CargaArchivo { get; set; }
    public DateTimeOffset FechaRegistro { get; set; }
}

/// <summary>Detalle auditable de una fila descartada o ajustada.</summary>
public sealed class DetalleCargaError
{
    public int Id { get; set; }
    public int CargaArchivoId { get; set; }
    public CargaArchivo? CargaArchivo { get; set; }
    public int NumeroFila { get; set; }
    public string? Periodo { get; set; }
    public string? CodigoProducto { get; set; }
    public string? Columna { get; set; }
    public MotivoRechazo Motivo { get; set; }
    public string? ValorCrudo { get; set; }
    public DateTimeOffset FechaRegistro { get; set; }
}

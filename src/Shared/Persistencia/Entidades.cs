using CargaMasiva.Domain;

namespace Persistencia;

/// <summary>Credencial de login (§2.3). El hash lo produce PasswordHasher, nunca texto plano.</summary>
public sealed class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool Activo { get; set; } = true;
}

/// <summary>§2.3d — refresh token con rotación: al usarse se revoca y se encadena al siguiente.</summary>
public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Token { get; set; } = "";
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
    public string? ReemplazadoPor { get; set; }

    public bool EstaActivo(DateTimeOffset ahora) => RevocadoEn is null && ExpiraEn > ahora;
}

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

    /// <summary>Transiciona validando contra la máquina de estados (specs/maquina-estados.md).</summary>
    public void Transicionar(EstadoCarga hacia)
    {
        MaquinaEstados.Validar(Estado, hacia);
        Estado = hacia;
        if (MaquinaEstados.EsTerminal(hacia) || hacia == EstadoCarga.Cargado)
            FechaFin ??= DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Un archivo trae N periodos (design.md §C3: el de muestra trae tres).
/// El veredicto por periodo es lo que permite el procesamiento parcial.
/// </summary>
public sealed class CargaPeriodo
{
    public int Id { get; set; }
    public int CargaArchivoId { get; set; }
    public CargaArchivo? CargaArchivo { get; set; }
    public string Periodo { get; set; } = "";
    public string Estado { get; set; } = EstadoPeriodo.Aceptado;
    public int FilasInsertadas { get; set; }
}

/// <summary>Estados de <see cref="CargaPeriodo"/>. El índice único parcial depende de <c>Aceptado</c>.</summary>
public static class EstadoPeriodo
{
    public const string Aceptado = "Aceptado";
    public const string YaCargado = "YaCargado";
    public const string Bloqueado = "Bloqueado";
}

/// <summary>Los registros del Excel que sí entraron. Clave de negocio: (Periodo, CodigoProducto) — §C5.</summary>
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

/// <summary>§3.3c — tabla de auditoría y trazabilidad de lo que no entró, y por qué.</summary>
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

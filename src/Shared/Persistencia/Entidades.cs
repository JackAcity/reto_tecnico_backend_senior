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

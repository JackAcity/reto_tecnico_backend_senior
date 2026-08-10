namespace Auth.Domain;

/// <summary>Credencial de login. El hash se obtiene mediante un adaptador de seguridad.</summary>
public sealed class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool Activo { get; set; } = true;
}

/// <summary>Refresh token con rotación y encadenamiento auditable.</summary>
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

using System.Security.Claims;
using System.Text;
using Auth.Domain;
using BuildingBlocks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api;

public sealed class OpcionesJwt
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpiraMinutos { get; set; } = 60;
    public int RefreshExpiraDias { get; set; } = 7;
}

public sealed class ProtectorContrasenas : IProtectorContrasenas
{
    private static readonly PasswordHasher<Usuario> Hasher = new();
    private static readonly Usuario UsuarioFicticio = new() { Id = -1, Email = "", Rol = "" };
    private static readonly string HashFicticio = Hasher.HashPassword(UsuarioFicticio, Guid.NewGuid().ToString());

    public VeredictoContrasena Verificar(Usuario usuario, string password) =>
        Hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password) switch
        {
            PasswordVerificationResult.Failed => VeredictoContrasena.Fallida,
            PasswordVerificationResult.SuccessRehashNeeded => VeredictoContrasena.RehashNecesario,
            _ => VeredictoContrasena.Valida
        };

    public void VerificarUsuarioInexistente(string password) =>
        Hasher.VerifyHashedPassword(UsuarioFicticio, HashFicticio, password);

    public string Hash(Usuario usuario, string password) => Hasher.HashPassword(usuario, password);
}

public sealed class EmisorJwt(IOptions<OpcionesJwt> opciones) : IEmisorAccessToken
{
    private readonly OpcionesJwt jwt = opciones.Value;

    public TimeSpan DuracionRefresh => TimeSpan.FromDays(jwt.RefreshExpiraDias);

    public TokenAcceso Emitir(Usuario usuario, DateTimeOffset ahora)
    {
        var expiraEn = ahora.AddMinutes(jwt.ExpiraMinutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", usuario.Rol)
        };
        claims.AddRange(ServicioAutenticacion.PermisosDe(usuario.Rol)
            .Select(p => new Claim(ServicioAutenticacion.ClaimPermiso, p)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = ahora.UtcDateTime,
            Expires = expiraEn.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(LlaveDeFirma(jwt.Key), SecurityAlgorithms.HmacSha256)
        };

        return new TokenAcceso(new JsonWebTokenHandler().CreateToken(descriptor), expiraEn);
    }

    public static SymmetricSecurityKey LlaveDeFirma(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < 32)
            throw new ExcepcionDeConfiguracion("Jwt:Key debe tener al menos 32 caracteres para HS256.");

        return new SymmetricSecurityKey(bytes);
    }
}

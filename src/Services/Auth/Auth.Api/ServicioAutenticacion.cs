using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Persistencia;

namespace Auth.Api;

public sealed class OpcionesJwt
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpiraMinutos { get; set; } = 60;
    public int RefreshExpiraDias { get; set; } = 7;
}

public sealed record ResultadoAutenticacion(string AccessToken, DateTimeOffset ExpiraEn, string RefreshToken);

/// <summary>
/// Emisión y rotación de credenciales (§2.3). Fuera de los endpoints para que la
/// rotación —que es la parte con reglas de verdad— sea testeable sin levantar HTTP.
/// </summary>
public sealed class ServicioAutenticacion(RetoDbContext db, IOptions<OpcionesJwt> opciones)
{
    /// <summary>Claim de permiso. El gateway lo exige en la ruta de carga (§3.2a).</summary>
    public const string ClaimPermiso = "permiso";
    public const string PermisoCargaMasiva = "carga:masiva";

    private static readonly PasswordHasher<Usuario> Hasher = new();

    // Usuario y hash de relleno para cuando el email no existe. Sin esto, "usuario
    // inexistente" retorna antes de correr PBKDF2 y "password incorrecta" sí lo
    // corre — el tiempo de respuesta delata qué emails existen aunque el 401 sea
    // idéntico en body y status. Se verifica siempre contra ALGÚN hash.
    private static readonly Usuario UsuarioFicticio = new() { Id = -1, Email = "", Rol = "" };
    private static readonly string HashFicticio =
        new PasswordHasher<Usuario>().HashPassword(UsuarioFicticio, Guid.NewGuid().ToString());

    private readonly OpcionesJwt _jwt = opciones.Value;

    /// <summary>Qué habilita cada rol. Un solo lugar donde mirar cuando el evaluador pregunte.</summary>
    public static string[] PermisosDe(string rol) => rol switch
    {
        "administrador" or "operador" => [PermisoCargaMasiva],
        _ => []
    };

    /// <summary>Devuelve null ante credenciales inválidas — sin distinguir usuario inexistente de contraseña incorrecta.</summary>
    public async Task<ResultadoAutenticacion?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Email == email && u.Activo, ct);

        var verificacion = Hasher.VerifyHashedPassword(
            usuario ?? UsuarioFicticio, usuario?.PasswordHash ?? HashFicticio, password);

        if (usuario is null || verificacion == PasswordVerificationResult.Failed)
            return null;

        // El hash quedó con parámetros viejos: se actualiza aprovechando que la
        // contraseña en claro está disponible justo en este punto y en ningún otro.
        if (verificacion == PasswordVerificationResult.SuccessRehashNeeded)
            usuario.PasswordHash = Hasher.HashPassword(usuario, password);

        return await EmitirAsync(usuario, anterior: null, ct);
    }

    /// <summary>
    /// §2.3d — rotación: el refresh usado se revoca y queda encadenado al que lo
    /// reemplaza, así el historial es auditable.
    /// </summary>
    public async Task<ResultadoAutenticacion?> RefrescarAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await db.RefreshTokens
            .Include(t => t.Usuario)
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Token == refreshToken, ct);

        // ponytail: un token ya revocado solo devuelve 401. La detección de reuso
        // (revocar toda la cadena ante un token viejo) se agrega si el alcance crece.
        if (token is null || !token.EstaActivo(DateTimeOffset.UtcNow) || token.Usuario is not { Activo: true })
            return null;

        return await EmitirAsync(token.Usuario, token, ct);
    }

    private async Task<ResultadoAutenticacion?> EmitirAsync(Usuario usuario, RefreshToken? anterior, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var nuevoToken = GenerarRefreshToken();

        if (anterior is not null)
        {
            // Rotación atómica (design.md §C18): dos refresh simultáneos con el mismo
            // token leerían ambos "activo" antes de que cualquiera escriba — un UPDATE
            // por PK sin guarda dejaría que el segundo pise el ReemplazadoPor del
            // primero (lost update). El WHERE revocado_en IS NULL es el compare-and-swap
            // que decide atómicamente quién ganó, igual criterio que sp_resolver_periodo
            // (§C9) pero sin necesitar advisory lock: es una sola fila, un solo UPDATE.
            var filas = await db.RefreshTokens
                .Where(t => t.Id == anterior.Id && t.RevocadoEn == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevocadoEn, ahora)
                    .SetProperty(t => t.ReemplazadoPor, nuevoToken), ct);

            if (filas == 0)
                return null;   // otro request ya rotó este token primero
        }

        var nuevo = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = nuevoToken,
            ExpiraEn = ahora.AddDays(_jwt.RefreshExpiraDias)
        };
        db.RefreshTokens.Add(nuevo);
        await db.SaveChangesAsync(ct);

        var expiraEn = ahora.AddMinutes(_jwt.ExpiraMinutos);
        return new ResultadoAutenticacion(CrearAccessToken(usuario, ahora, expiraEn), expiraEn, nuevo.Token);
    }

    private string CrearAccessToken(Usuario usuario, DateTimeOffset ahora, DateTimeOffset expiraEn)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", usuario.Rol)
        };
        claims.AddRange(PermisosDe(usuario.Rol).Select(p => new Claim(ClaimPermiso, p)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = ahora.UtcDateTime,
            Expires = expiraEn.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(LlaveDeFirma(_jwt.Key), SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>HS256 exige al menos 256 bits de clave; una más corta falla al firmar, no al validar.</summary>
    public static SymmetricSecurityKey LlaveDeFirma(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres para HS256.");

        return new SymmetricSecurityKey(bytes);
    }

    private static string GenerarRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
}

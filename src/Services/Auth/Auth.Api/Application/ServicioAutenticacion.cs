using System.Security.Cryptography;
using Auth.Domain;

namespace Auth.Api;

public sealed record ResultadoAutenticacion(string AccessToken, DateTimeOffset ExpiraEn, string RefreshToken);

public enum VeredictoContrasena { Fallida, Valida, RehashNecesario }

public interface IProtectorContrasenas
{
    VeredictoContrasena Verificar(Usuario usuario, string password);
    void VerificarUsuarioInexistente(string password);
    string Hash(Usuario usuario, string password);
}

public sealed record TokenAcceso(string Valor, DateTimeOffset ExpiraEn);

public interface IEmisorAccessToken
{
    TimeSpan DuracionRefresh { get; }
    TokenAcceso Emitir(Usuario usuario, DateTimeOffset ahora);
}

/// <summary>
/// Emisión y rotación de credenciales (§2.3). Fuera de los endpoints para que la
/// rotación —que es la parte con reglas de verdad— sea testeable sin levantar HTTP.
/// </summary>
public sealed class ServicioAutenticacion(
    IRepositorioUsuarios repositorio,
    IProtectorContrasenas contrasenas,
    IEmisorAccessToken emisor)
{
    /// <summary>Claim de permiso. El gateway lo exige en la ruta de carga (§3.2a).</summary>
    public const string ClaimPermiso = "permiso";
    public const string PermisoCargaMasiva = "carga:masiva";

    /// <summary>Qué habilita cada rol. Un solo lugar donde mirar cuando el evaluador pregunte.</summary>
    public static string[] PermisosDe(string rol) => rol switch
    {
        "administrador" or "operador" => [PermisoCargaMasiva],
        _ => []
    };

    /// <summary>Devuelve null ante credenciales inválidas — sin distinguir usuario inexistente de contraseña incorrecta.</summary>
    public async Task<ResultadoAutenticacion?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var usuario = await repositorio.ObtenerPorEmailActivoAsync(email, ct);

        if (usuario is null)
        {
            contrasenas.VerificarUsuarioInexistente(password);
            return null;
        }

        var verificacion = contrasenas.Verificar(usuario, password);
        if (verificacion == VeredictoContrasena.Fallida)
            return null;

        // El hash quedó con parámetros viejos: se actualiza aprovechando que la
        // contraseña en claro está disponible justo en este punto y en ningún otro.
        if (verificacion == VeredictoContrasena.RehashNecesario)
            usuario.PasswordHash = contrasenas.Hash(usuario, password);

        return await EmitirAsync(usuario, anterior: null, ct);
    }

    /// <summary>
    /// §2.3d — rotación: el refresh usado se revoca y queda encadenado al que lo
    /// reemplaza, así el historial es auditable.
    /// </summary>
    public async Task<ResultadoAutenticacion?> RefrescarAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await repositorio.ObtenerRefreshTokenConUsuarioAsync(refreshToken, ct);

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
            var filas = await repositorio.RevocarSiActivoAsync(anterior.Id, nuevoToken, ahora, ct);

            if (filas == 0)
                return null;   // otro request ya rotó este token primero
        }

        var nuevo = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = nuevoToken,
            ExpiraEn = ahora.Add(emisor.DuracionRefresh)
        };
        repositorio.AgregarRefreshToken(nuevo);
        await repositorio.GuardarCambiosAsync(ct);

        var accessToken = emisor.Emitir(usuario, ahora);
        return new ResultadoAutenticacion(accessToken.Valor, accessToken.ExpiraEn, nuevo.Token);
    }

    private static string GenerarRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

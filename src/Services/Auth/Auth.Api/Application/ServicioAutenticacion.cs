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

/// <summary>Emite y rota credenciales sin depender del transporte HTTP.</summary>
public sealed class ServicioAutenticacion(
    IRepositorioUsuarios repositorio,
    IProtectorContrasenas contrasenas,
    IEmisorAccessToken emisor)
{
    /// <summary>Nombre del claim que autoriza la carga masiva.</summary>
    public const string ClaimPermiso = "permiso";
    public const string PermisoCargaMasiva = "carga:masiva";

    /// <summary>Permisos concedidos a cada rol.</summary>
    public static string[] PermisosDe(string rol) => rol switch
    {
        "administrador" or "operador" => [PermisoCargaMasiva],
        _ => []
    };

    /// <summary>No distingue usuario inexistente de contraseña incorrecta.</summary>
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

        // Rehash sólo mientras la contraseña en claro está disponible.
        if (verificacion == VeredictoContrasena.RehashNecesario)
            usuario.PasswordHash = contrasenas.Hash(usuario, password);

        return await EmitirAsync(usuario, anterior: null, ct);
    }

    /// <summary>Rota el refresh token y enlaza el reemplazo para auditoría.</summary>
    public async Task<ResultadoAutenticacion?> RefrescarAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await repositorio.ObtenerRefreshTokenConUsuarioAsync(refreshToken, ct);

        // Un token revocado recibe la misma respuesta que una credencial inválida.
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
            // El update condicional evita que dos refresh roten el mismo token.
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

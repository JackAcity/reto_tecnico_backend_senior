using Auth.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Persistencia;

namespace CargaMasiva.Tests;

/// <summary>
/// §2.3 — login, claims del JWT y rotación del refresh token.
/// Requiere la base levantada; todo corre en una transacción que se revierte.
/// </summary>
public sealed class AutenticacionTests : IAsyncLifetime
{
    private const string Password = "Reto2026!";

    private static readonly OpcionesJwt Jwt = new()
    {
        Key = "clave_de_pruebas_con_mas_de_32_caracteres",
        Issuer = "reto-auth",
        Audience = "reto-api",
        ExpiraMinutos = 60,
        RefreshExpiraDias = 7
    };

    private static string Cadena =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Database=reto;Username=reto;Password=cambiar_en_local";

    private readonly string _email = $"login-{Guid.NewGuid():N}@reto.local";
    private NpgsqlConnection _cn = null!;
    private RetoDbContext _db = null!;
    private ServicioAutenticacion _svc = null!;

    public async Task InitializeAsync()
    {
        _cn = new NpgsqlConnection(Cadena);
        await _cn.OpenAsync();
        _db = new RetoDbContext(new DbContextOptionsBuilder<RetoDbContext>()
            .UseNpgsql(_cn)
            .UseSnakeCaseNamingConvention()
            .Options);
        await _db.Database.BeginTransactionAsync();
        _svc = new ServicioAutenticacion(_db, Options.Create(Jwt));

        await _db.SembrarUsuarioAsync(_email, Password, "administrador");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.RollbackTransactionAsync();
        await _db.DisposeAsync();
        await _cn.DisposeAsync();
    }

    private static async Task<TokenValidationResult> ValidarAsync(string accessToken) =>
        await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = ServicioAutenticacion.LlaveDeFirma(Jwt.Key),
            RoleClaimType = "role"
        });

    [Fact]
    public async Task Login_ConCredencialesValidas_EmiteTokenFirmadoConSusClaims()
    {
        var resultado = await _svc.LoginAsync(_email, Password);

        Assert.NotNull(resultado);
        var validacion = await ValidarAsync(resultado.AccessToken);
        Assert.True(validacion.IsValid);

        var usuario = await _db.Usuarios.SingleAsync(u => u.Email == _email);
        Assert.Equal(usuario.Id.ToString(), validacion.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(_email, validacion.Claims[JwtRegisteredClaimNames.Email]);
        Assert.Equal("administrador", validacion.Claims["role"]);
        // §3.2a — el permiso viaja en el token; el gateway lo exige en la ruta de carga.
        Assert.Equal(ServicioAutenticacion.PermisoCargaMasiva, validacion.Claims[ServicioAutenticacion.ClaimPermiso]);
    }

    [Theory]
    [InlineData("clave-incorrecta")]
    [InlineData("")]
    public async Task Login_ConPasswordIncorrecta_NoEmiteNada(string password)
    {
        Assert.Null(await _svc.LoginAsync(_email, password));
    }

    [Fact]
    public async Task Login_DeUsuarioInexistente_NoEmiteNada()
    {
        Assert.Null(await _svc.LoginAsync("no-existe@reto.local", Password));
    }

    [Fact]
    public async Task Login_DeUsuarioInactivo_NoEmiteNada()
    {
        var usuario = await _db.Usuarios.SingleAsync(u => u.Email == _email);
        usuario.Activo = false;
        await _db.SaveChangesAsync();

        Assert.Null(await _svc.LoginAsync(_email, Password));
    }

    [Fact]
    public async Task Refresh_RotaElToken_YRevocaElAnterior()
    {
        var inicial = (await _svc.LoginAsync(_email, Password))!;

        var rotado = await _svc.RefrescarAsync(inicial.RefreshToken);

        Assert.NotNull(rotado);
        Assert.NotEqual(inicial.RefreshToken, rotado.RefreshToken);
        Assert.True((await ValidarAsync(rotado.AccessToken)).IsValid);

        var anterior = await _db.RefreshTokens.SingleAsync(t => t.Token == inicial.RefreshToken);
        Assert.NotNull(anterior.RevocadoEn);
        Assert.Equal(rotado.RefreshToken, anterior.ReemplazadoPor);
    }

    [Fact]
    public async Task Refresh_ConTokenYaUsado_Falla()
    {
        var inicial = (await _svc.LoginAsync(_email, Password))!;
        await _svc.RefrescarAsync(inicial.RefreshToken);

        Assert.Null(await _svc.RefrescarAsync(inicial.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ConTokenExpirado_Falla()
    {
        var inicial = (await _svc.LoginAsync(_email, Password))!;
        var token = await _db.RefreshTokens.SingleAsync(t => t.Token == inicial.RefreshToken);
        token.ExpiraEn = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();

        Assert.Null(await _svc.RefrescarAsync(inicial.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ConTokenDesconocido_Falla()
    {
        Assert.Null(await _svc.RefrescarAsync("token-que-no-existe"));
    }

    [Fact]
    public void RolSinPermisoDeCarga_NoRecibeElClaim()
    {
        Assert.Empty(ServicioAutenticacion.PermisosDe("consulta"));
        Assert.Contains(ServicioAutenticacion.PermisoCargaMasiva, ServicioAutenticacion.PermisosDe("operador"));
    }
}

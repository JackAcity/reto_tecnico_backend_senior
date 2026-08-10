using Microsoft.AspNetCore.Identity;
using Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Persistencia;

namespace Reto.Tests;

/// <summary>
/// El seed corre en cada arranque de Auth. Si no fuera idempotente, el segundo
/// <c>docker compose up</c> dejaría el servicio en crash loop.
/// Requiere la base levantada; se revierte al terminar.
/// </summary>
public sealed class SemillaTests : IAsyncLifetime
{
    private static string Cadena =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Database=reto;Username=reto;Password=cambiar_en_local";

    private readonly string _email = $"semilla-{Guid.NewGuid():N}@reto.local";
    private NpgsqlConnection _cn = null!;
    private RetoDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _cn = new NpgsqlConnection(Cadena);
        await _cn.OpenAsync();
        _db = new RetoDbContext(new DbContextOptionsBuilder<RetoDbContext>()
            .UseNpgsql(_cn)
            .UseSnakeCaseNamingConvention()
            .Options);
        await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.RollbackTransactionAsync();
        await _db.DisposeAsync();
        await _cn.DisposeAsync();
    }

    [Fact]
    public async Task SembrarDosVeces_DejaUnSoloUsuario()
    {
        Assert.True(await _db.SembrarUsuarioAsync(_email, "Reto2026!", "administrador"));
        Assert.False(await _db.SembrarUsuarioAsync(_email, "Reto2026!", "administrador"));

        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == _email));
    }

    [Fact]
    public async Task Contrasena_SeGuardaHasheada_YVerifica()
    {
        await _db.SembrarUsuarioAsync(_email, "Reto2026!", "administrador");
        var usuario = await _db.Usuarios.SingleAsync(u => u.Email == _email);

        Assert.NotEqual("Reto2026!", usuario.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<Usuario>().VerifyHashedPassword(usuario, usuario.PasswordHash, "Reto2026!"));
    }
}

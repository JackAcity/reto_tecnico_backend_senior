using Auth.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Persistencia;

/// <summary>
/// Usuario inicial. Sin él, una base recién creada no permite obtener un JWT y el
/// sistema entero es inalcanzable — incluida la demo en video.
/// La escribe <b>solo Auth</b>, dueño de la tabla <c>usuario</c> (db-schema.md).
/// </summary>
public static class Semilla
{
    /// <summary>
    /// Crea el usuario si no existe. Idempotente: el arranque se repite en cada
    /// <c>docker compose up</c> y no debe fallar ni duplicar.
    /// </summary>
    public static async Task<bool> SembrarUsuarioAsync(
        this RetoDbContext db, string email, string password, string rol, CancellationToken ct = default)
    {
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return false;

        var usuario = new Usuario { Email = email, Rol = rol, Activo = true };
        // §4.x — nunca contraseña en texto plano. PBKDF2 con salt por usuario.
        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, password);
        db.Usuarios.Add(usuario);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Dos instancias sembrando a la vez: el índice único sobre email decide.
            // El perdedor sigue arrancando, no entra en crash loop.
            db.Entry(usuario).State = EntityState.Detached;
            return false;
        }
    }
}

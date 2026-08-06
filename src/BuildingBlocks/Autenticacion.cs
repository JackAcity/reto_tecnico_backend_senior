using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks;

/// <summary>
/// Validación de JWT y políticas, compartidas por el gateway y por Control.
/// El gateway ya filtra, pero Control vuelve a validar: la auditoría de "quién
/// subió el archivo" sale del token, y confiar en una cabecera inyectada haría
/// que cualquiera con acceso a la red interna pudiera falsear al usuario.
/// </summary>
public static class Autenticacion
{
    public const string ClaimPermiso = "permiso";
    public const string PermisoCargaMasiva = "carga:masiva";
    public const string PoliticaAutenticado = "autenticado";
    public const string PoliticaCargaMasiva = "cargaMasiva";

    public static IServiceCollection AddAutenticacionJwt(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Sin el mapeo heredado los claims conservan su nombre original
                // ("sub", "role", "permiso"), que es como se emiten y como los lee
                // el particionado del rate limiter.
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key."))),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        servicios.AddAuthorizationBuilder()
            .AddPolicy(PoliticaAutenticado, p => p.RequireAuthenticatedUser())
            // §3.2a — no basta con estar autenticado: hay que tener el permiso de carga.
            .AddPolicy(PoliticaCargaMasiva, p => p
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimPermiso, PermisoCargaMasiva));

        return servicios;
    }
}

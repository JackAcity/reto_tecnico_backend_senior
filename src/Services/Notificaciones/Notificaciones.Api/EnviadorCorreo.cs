using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Notificaciones.Api;

public sealed class OpcionesSmtp
{
    public string Host { get; set; } = "";
    public int Puerto { get; set; }
    public string Usuario { get; set; } = "";
    public string Password { get; set; } = "";
    public string Desde { get; set; } = "";
    public bool UsarSsl { get; set; }

    /// <summary>
    /// Usuario/Password sin validar: Mailpit (la demo) no exige credenciales —
    /// un SMTP real sí. Host/Puerto/Desde sí son obligatorios en cualquier caso.
    /// </summary>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new InvalidOperationException("Falta Smtp:Host.");
        if (Puerto <= 0) throw new InvalidOperationException("Falta Smtp:Puerto.");
        if (string.IsNullOrWhiteSpace(Desde)) throw new InvalidOperationException("Falta Smtp:Desde.");
    }
}

/// <summary>§4️⃣/§6️⃣ — MailKit, configurable por variables de entorno (único requisito literal del enunciado sobre correo).</summary>
public sealed class EnviadorCorreoMailKit(IOptions<OpcionesSmtp> opciones) : IEnviadorCorreo
{
    private readonly OpcionesSmtp _smtp = opciones.Value;

    public async Task EnviarResumenCargaAsync(
        string destinatario, int idCarga, int filasInsertadas, int filasRechazadas, DateTimeOffset fechaFin, CancellationToken ct = default)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(_smtp.Desde));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = $"Carga #{idCarga} finalizada";
        mensaje.Body = new TextPart("plain")
        {
            Text = $"""
                Tu carga #{idCarga} terminó de procesarse el {fechaFin:yyyy-MM-dd HH:mm} UTC.

                Filas insertadas: {filasInsertadas}
                Filas rechazadas: {filasRechazadas}

                Consultá el detalle completo (incluidos los motivos de rechazo) en GET /cargas/{idCarga}.
                """
        };

        using var cliente = new SmtpClient();
        // Mailpit no pide TLS ni credenciales; un SMTP real sí — ambos casos
        // conviven con la misma configuración por variables de entorno (§4.19).
        await cliente.ConnectAsync(_smtp.Host, _smtp.Puerto,
            _smtp.UsarSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None, ct);

        if (!string.IsNullOrWhiteSpace(_smtp.Usuario))
            await cliente.AuthenticateAsync(_smtp.Usuario, _smtp.Password, ct);

        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }
}

public static class EnviadorCorreoExtensiones
{
    public static IServiceCollection AddEnviadorCorreo(this IServiceCollection servicios, IConfiguration config)
    {
        servicios.Configure<OpcionesSmtp>(config.GetSection("Smtp"));
        servicios.AddTransient<IEnviadorCorreo, EnviadorCorreoMailKit>();
        return servicios;
    }
}

using Notificaciones.Api;

namespace Reto.Tests;

/// <summary>Falla al arrancar y no en el primer correo — mismo criterio que RabbitMq/SeaweedFs/Jwt.</summary>
public class OpcionesSmtpTests
{
    private static OpcionesSmtp Validas() => new() { Host = "mailpit", Puerto = 1025, Desde = "no-reply@reto.local" };

    [Fact]
    public void ConfiguracionCompleta_NoLanza() => Validas().Validar();

    [Fact]
    public void UsuarioYPasswordSonOpcionales_MailpitNoLosExige()
    {
        var opciones = Validas();
        opciones.Usuario = "";
        opciones.Password = "";

        opciones.Validar();
    }

    [Fact]
    public void SinHost_Lanza()
    {
        var opciones = Validas();
        opciones.Host = "";

        Assert.Throws<InvalidOperationException>(opciones.Validar);
    }

    [Fact]
    public void SinPuerto_Lanza()
    {
        var opciones = Validas();
        opciones.Puerto = 0;

        Assert.Throws<InvalidOperationException>(opciones.Validar);
    }

    [Fact]
    public void SinDesde_Lanza()
    {
        var opciones = Validas();
        opciones.Desde = "";

        Assert.Throws<InvalidOperationException>(opciones.Validar);
    }
}

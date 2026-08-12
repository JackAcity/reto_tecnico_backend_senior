namespace Reto.Tests;

/// <summary>
/// Cierra guardia-arquitectura-dip (design.md §D5 de arquitectura-hexagonal-transversal):
/// Application/Domain de ningún servicio referencia infraestructura concreta. Dos
/// técnicas según el tipo de límite — reflection real donde las capas son ensamblados
/// separados (CargaMasiva), escaneo de texto donde son carpetas dentro del mismo
/// ensamblado (Auth, Control, Notificaciones: no hay límite de assembly que reflejar
/// ahí). Sin dependencia nueva — <c>System.Reflection</c>/<c>System.IO</c> ya están.
/// </summary>
public sealed class GuardiaArquitecturaTests
{
    private static readonly string[] PaquetesProhibidos =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "RabbitMQ.Client"];

    private static readonly string RaizRepo =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Proyecto(params string[] segmentos) => Path.Combine([RaizRepo, .. segmentos]);

    [Theory]
    [InlineData(typeof(CargaMasiva.Application.ManejadorCarga))]
    [InlineData(typeof(CargaMasiva.Domain.EstadoCarga))]
    [InlineData(typeof(Auth.Api.ServicioAutenticacion))]
    [InlineData(typeof(Auth.Domain.Usuario))]
    [InlineData(typeof(Control.Api.ServicioCargas))]
    [InlineData(typeof(Notificaciones.Api.ManejadorNotificacion))]
    public void NucleosYApplication_NoReferencianInfraestructuraConcreta(Type tipoDeMarca)
    {
        var referenciadas = tipoDeMarca.Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        foreach (var prohibido in PaquetesProhibidos)
            Assert.DoesNotContain(prohibido, referenciadas);
    }

    [Theory]
    [InlineData("src/Services/CargaMasiva/CargaMasiva.Application/CargaMasiva.Application.csproj")]
    [InlineData("src/Services/Auth/Auth.Application/Auth.Application.csproj")]
    [InlineData("src/Services/Control/Control.Application/Control.Application.csproj")]
    [InlineData("src/Services/Notificaciones/Notificaciones.Application/Notificaciones.Application.csproj")]
    public void Application_NoReferenciaAdaptadoresCompartidos(string rutaProyecto)
    {
        var proyecto = File.ReadAllText(Path.Combine(RaizRepo, rutaProyecto.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("Shared\\Almacenamiento", proyecto, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared\\Mensajeria", proyecto, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared\\Persistencia", proyecto, StringComparison.Ordinal);
    }

    [Fact]
    public void NucleoNoTieneFrameworksNiPaquetes()
    {
        var nucleo = File.ReadAllText(Proyecto("src", "BuildingBlocks", "BuildingBlocks.csproj"));

        Assert.DoesNotContain("FrameworkReference", nucleo, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference", nucleo, StringComparison.Ordinal);

        var contratos = File.ReadAllText(Proyecto("src", "BuildingBlocks", "Mensajes.cs"));
        Assert.DoesNotContain("JsonConverter", contratos, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", contratos, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Auth", "Auth.Api")]
    [InlineData("Control", "Control.Api")]
    [InlineData("Notificaciones", "Notificaciones.Api")]
    public void CarpetaApplication_NoContieneUsingsDeInfraestructuraConcreta(string carpetaServicio, string nombreProyecto)
    {
        var carpeta = Path.Combine(RaizRepo, "src", "Services", carpetaServicio, nombreProyecto, "Application");
        Assert.True(Directory.Exists(carpeta), $"No se encontró {carpeta}");

        foreach (var archivo in Directory.GetFiles(carpeta, "*.cs"))
        {
            var contenido = File.ReadAllText(archivo);
            foreach (var prohibido in PaquetesProhibidos)
                Assert.False(
                    contenido.Contains($"using {prohibido}", StringComparison.Ordinal),
                    $"{Path.GetFileName(archivo)} referencia infraestructura concreta: using {prohibido}");

            Assert.DoesNotContain("using Almacenamiento;", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("using Mensajeria;", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("using Persistencia;", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("using Microsoft.AspNetCore.Identity;", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("using Microsoft.Extensions.Options;", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("using Microsoft.IdentityModel", contenido, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SeaweedFs_SeRegistraSoloEnLosServiciosQueLoPoseen()
    {
        var solucion = File.ReadAllText(Proyecto("Reto.slnx"));
        Assert.DoesNotContain("Shared/Almacenamiento", solucion, StringComparison.Ordinal);

        var proyectos = Directory.GetFiles(Proyecto("src"), "*.csproj", SearchOption.AllDirectories);
        foreach (var proyecto in proyectos)
        {
            var contenido = File.ReadAllText(proyecto);
            Assert.DoesNotContain("Shared\\Almacenamiento", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("Shared/Almacenamiento", contenido, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ControlApplication_NoReferenciaElDominioDeOtroServicio()
    {
        var proyecto = File.ReadAllText(Proyecto("src", "Services", "Control", "Control.Application", "Control.Application.csproj"));

        Assert.DoesNotContain("CargaMasiva.Domain.csproj", proyecto, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificacionesApplication_NoReferenciaElDominioDeOtroServicio()
    {
        var proyecto = File.ReadAllText(Proyecto("src", "Services", "Notificaciones", "Notificaciones.Application", "Notificaciones.Application.csproj"));

        Assert.DoesNotContain("CargaMasiva.Domain.csproj", proyecto, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistenciaCompartida_NoFormaParteDeLaSolucion()
    {
        Assert.False(File.Exists(Proyecto("src", "Shared", "Persistencia", "Persistencia.csproj")));

        var solucion = File.ReadAllText(Proyecto("Reto.slnx"));
        Assert.DoesNotContain("Shared/Persistencia", solucion, StringComparison.Ordinal);
    }

    [Fact]
    public void MensajeriaCompartida_NoFormaParteDeLaSolucion()
    {
        Assert.False(File.Exists(Proyecto("src", "Shared", "Mensajeria", "Mensajeria.csproj")));

        var solucion = File.ReadAllText(Proyecto("Reto.slnx"));
        Assert.DoesNotContain("Shared/Mensajeria", solucion, StringComparison.Ordinal);
    }

    [Fact]
    public void NingunProyectoReferenciaAdaptadoresSharedEliminados()
    {
        foreach (var proyecto in Directory.GetFiles(Proyecto("src"), "*.csproj", SearchOption.AllDirectories))
        {
            var contenido = File.ReadAllText(proyecto);
            Assert.DoesNotContain("Shared\\Almacenamiento", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("Shared\\Mensajeria", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("Shared\\Persistencia", contenido, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Auth", "CargaMasiva")]
    [InlineData("Auth", "Control")]
    [InlineData("Auth", "Notificaciones")]
    [InlineData("CargaMasiva", "Auth")]
    [InlineData("CargaMasiva", "Control")]
    [InlineData("CargaMasiva", "Notificaciones")]
    [InlineData("Control", "Auth")]
    [InlineData("Control", "CargaMasiva")]
    [InlineData("Control", "Notificaciones")]
    [InlineData("Notificaciones", "Auth")]
    [InlineData("Notificaciones", "CargaMasiva")]
    [InlineData("Notificaciones", "Control")]
    public void ServiciosNoTienenReferenciasDeCompilacionEntreSi(string servicioOrigen, string servicioDestino)
    {
        var carpetaOrigen = Proyecto("src", "Services", servicioOrigen);
        foreach (var proyecto in Directory.GetFiles(carpetaOrigen, "*.csproj", SearchOption.AllDirectories))
        {
            var contenido = File.ReadAllText(proyecto);
            Assert.DoesNotContain($"Services\\{servicioDestino}\\", contenido, StringComparison.Ordinal);
            Assert.DoesNotContain($"Services/{servicioDestino}/", contenido, StringComparison.Ordinal);
        }
    }
}

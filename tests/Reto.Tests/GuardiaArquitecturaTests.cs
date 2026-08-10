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

    [Theory]
    [InlineData(typeof(CargaMasiva.Application.ManejadorCarga))]
    [InlineData(typeof(CargaMasiva.Domain.EstadoCarga))]
    public void CargaMasiva_ApplicationYDomain_NoReferencianInfraestructuraConcreta(Type tipoDeMarca)
    {
        var referenciadas = tipoDeMarca.Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        foreach (var prohibido in PaquetesProhibidos)
            Assert.DoesNotContain(prohibido, referenciadas);
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
        }
    }
}

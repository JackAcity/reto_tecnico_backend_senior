using ServiceHost;

namespace Reto.Tests;

/// <summary>
/// El CorrelationId entrante lo controla el cliente y cae directo en el log de
/// consola (Serilog). Sin validar, un valor con saltos de línea permite inyectar
/// líneas de log falsas.
/// </summary>
public class CorrelationIdTests
{
    [Theory]
    [InlineData("a1b2c3d4e5f64a3b8c9d0e1f2a3b4c5d")]   // Guid.ToString("N")
    [InlineData("test-123")]
    public void ValorConFormatoSeguro_SeConservaTalCual(string entrante)
    {
        Assert.Equal(entrante, ServiceDefaults.CorrelationIdSeguro(entrante));
    }

    [Theory]
    [InlineData("valor\nfalso] [INF] Usuario admin autenticado\n[10:00:00 WRN")]
    [InlineData("valor con espacios")]
    [InlineData("<script>alert(1)</script>")]
    public void ValorSospechoso_SeDescartaYSeGeneraUnoNuevo(string entrante)
    {
        Assert.NotEqual(entrante, ServiceDefaults.CorrelationIdSeguro(entrante));
    }

    [Fact]
    public void ValorDemasiadoLargo_SeDescarta()
    {
        var largo = new string('a', 65);

        Assert.NotEqual(largo, ServiceDefaults.CorrelationIdSeguro(largo));
    }

    [Fact]
    public void SinValorEntrante_GeneraUnoNuevo()
    {
        Assert.NotNull(ServiceDefaults.CorrelationIdSeguro(null));
    }
}

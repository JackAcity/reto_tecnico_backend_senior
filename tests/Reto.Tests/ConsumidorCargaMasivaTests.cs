using CargaMasiva.Infrastructure;

namespace Reto.Tests;

/// <summary>
/// Puro: no necesita RabbitMQ. Reproduce la forma exacta en que RabbitMQ.Client
/// decodifica x-death. Se verifica contra el adaptador local de CargaMasiva; Notificaciones protege el mismo contrato desde su propio adaptador.
/// </summary>
public class ContarIntentosPreviosTests
{
    private static Dictionary<string, object?> TablaMuerte(string cola, long veces) => new()
    {
        ["queue"] = System.Text.Encoding.UTF8.GetBytes(cola),
        ["count"] = veces
    };

    [Fact]
    public void SinHeaders_CeroIntentos() =>
        Assert.Equal(0, TopologiaRabbit.ContarIntentosPrevios(null, TopologiaRabbit.ColaCarga));

    [Fact]
    public void SinXDeath_CeroIntentos() =>
        Assert.Equal(0, TopologiaRabbit.ContarIntentosPrevios(new Dictionary<string, object?>(), TopologiaRabbit.ColaCarga));

    [Fact]
    public void ConXDeathDeLaColaDeCarga_DevuelveElCount()
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-death"] = new List<object?> { TablaMuerte("carga_masiva", 2) }
        };

        Assert.Equal(2, TopologiaRabbit.ContarIntentosPrevios(headers, TopologiaRabbit.ColaCarga));
    }

    [Fact]
    public void ConXDeathDeOtraCola_SeIgnora()
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-death"] = new List<object?> { TablaMuerte("otra.cola", 5) }
        };

        Assert.Equal(0, TopologiaRabbit.ContarIntentosPrevios(headers, TopologiaRabbit.ColaCarga));
    }

    [Fact]
    public void ConVariasEntradas_TomaElMaximoDeLaColaCorrecta()
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-death"] = new List<object?>
            {
                TablaMuerte("otra.cola", 9),
                TablaMuerte("carga_masiva", 1)
            }
        };

        Assert.Equal(1, TopologiaRabbit.ContarIntentosPrevios(headers, TopologiaRabbit.ColaCarga));
    }

    [Fact]
    public void DistingueEntreColaDeCargaYDeNotificaciones()
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-death"] = new List<object?> { TablaMuerte("notificaciones", 3) }
        };

        Assert.Equal(0, TopologiaRabbit.ContarIntentosPrevios(headers, TopologiaRabbit.ColaCarga));
        Assert.Equal(3, TopologiaRabbit.ContarIntentosPrevios(headers, TopologiaRabbit.ColaNotificaciones));
    }
}

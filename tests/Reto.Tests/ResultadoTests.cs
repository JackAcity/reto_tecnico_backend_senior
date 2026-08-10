using BuildingBlocks;

namespace Reto.Tests;

/// <summary>Cierra la Requirement "Resultado<T> vive como tipo compartido" (resultado-sin-excepciones).</summary>
public sealed class ResultadoTests
{
    [Fact]
    public void Exito_expone_valor_y_ningun_error()
    {
        var resultado = Resultado<int>.Exito(42);

        Assert.True(resultado.EsExitoso);
        Assert.Equal(42, resultado.Valor);
        Assert.Null(resultado.Error);
    }

    [Fact]
    public void Fallo_expone_error_y_ningun_valor()
    {
        var resultado = Resultado<int>.Fallo("no se pudo procesar");

        Assert.False(resultado.EsExitoso);
        Assert.Equal(0, resultado.Valor);
        Assert.Equal("no se pudo procesar", resultado.Error);
    }

    [Fact]
    public void Resultado_sin_valor_exito()
    {
        var resultado = Resultado.Exito();

        Assert.True(resultado.EsExitoso);
        Assert.Null(resultado.Error);
    }

    [Fact]
    public void Resultado_sin_valor_fallo()
    {
        var resultado = Resultado.Fallo("fallo de publicación");

        Assert.False(resultado.EsExitoso);
        Assert.Equal("fallo de publicación", resultado.Error);
    }
}

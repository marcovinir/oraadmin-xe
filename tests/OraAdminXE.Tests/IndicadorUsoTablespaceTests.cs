using OraAdminXE.Modulos.Tablespaces;
using OraAdminXE.UI;

namespace OraAdminXE.Tests;

public class IndicadorUsoTablespaceTests
{
    [Theory]
    [InlineData(0, TipoEstado.Ok)]
    [InlineData(69.999, TipoEstado.Ok)]
    [InlineData(70, TipoEstado.Advertencia)]
    [InlineData(85, TipoEstado.Advertencia)]
    [InlineData(85.001, TipoEstado.Peligro)]
    [InlineData(100, TipoEstado.Peligro)]
    public void Clasificar_respeta_los_umbral_exactos(double porcentaje, TipoEstado esperado) =>
        Assert.Equal(esperado, IndicadorUsoTablespace.Clasificar(porcentaje));

    [Fact]
    public void Clasificar_sin_porcentaje_es_neutro() =>
        Assert.Equal(TipoEstado.Neutro, IndicadorUsoTablespace.Clasificar(null));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(101, 100)]
    public void Normalizar_limita_el_rango_de_dibujo(double entrada, double esperado) =>
        Assert.Equal((double?)esperado, IndicadorUsoTablespace.Normalizar(entrada));
}

using OraAdminXE.Datos;

namespace OraAdminXE.Tests;

public class IdentificadorTests
{
    [Theory]
    [InlineData("ts_ventas", "TS_VENTAS")]
    [InlineData("  Juan  ", "JUAN")]
    [InlineData("A$B#1", "A$B#1")]
    public void Validar_acepta_nombres_simples_y_los_pasa_a_mayusculas(string entrada, string esperado) =>
        Assert.Equal(esperado, Identificador.Validar(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("juan; DROP USER sys")]
    [InlineData("a b")]
    [InlineData("x\"y")]
    public void Validar_rechaza_nombres_peligrosos(string entrada) =>
        Assert.Throws<ArgumentException>(() => Identificador.Validar(entrada));

    [Fact]
    public void Literal_escapa_comillas() =>
        Assert.Equal("'C:\\o''brien'", Identificador.Literal("C:\\o'brien"));
}

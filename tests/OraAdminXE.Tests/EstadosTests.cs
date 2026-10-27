using OraAdminXE.UI;

namespace OraAdminXE.Tests;

public class EstadosTests
{
    [Theory]
    [InlineData("OPEN", TipoEstado.Ok)]
    [InlineData("COMPLETADO", TipoEstado.Ok)]
    [InlineData("ONLINE", TipoEstado.Ok)]
    [InlineData("YES", TipoEstado.Ok)]
    [InlineData("LOCKED", TipoEstado.Peligro)]
    [InlineData("LOCKED(TIMED)", TipoEstado.Peligro)]
    [InlineData("EXPIRED & LOCKED", TipoEstado.Peligro)]
    [InlineData("ERROR", TipoEstado.Peligro)]
    [InlineData("EXPIRED", TipoEstado.Advertencia)]
    [InlineData("EXPIRED(GRACE)", TipoEstado.Advertencia)]
    [InlineData("EN_PROCESO", TipoEstado.Advertencia)]
    [InlineData("READ ONLY", TipoEstado.Advertencia)]
    [InlineData("NO", TipoEstado.Neutro)]
    [InlineData("ELIMINADO", TipoEstado.Neutro)]
    [InlineData("PERMANENT", TipoEstado.Info)]
    public void Clasificar(string texto, TipoEstado esperado) => Assert.Equal(esperado, Estados.Clasificar(texto));
}

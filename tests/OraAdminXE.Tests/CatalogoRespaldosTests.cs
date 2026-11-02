using OraAdminXE.Datos;
using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Tests;

/// <summary>Catálogo, progreso en vivo y log de respaldos (issue #12).</summary>
public class CatalogoRespaldosTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(0, "0 s")]
    [InlineData(59, "59 s")]
    [InlineData(140, "2 min 20 s")]
    [InlineData(605, "10 min 05 s")]
    public void Duracion_legible(int segundos, string esperado) =>
        Assert.Equal(esperado, UcCatalogoRespaldos.Duracion(segundos));

    [Fact]
    public void Cuenta_las_lineas_con_errores_de_Oracle()
    {
        const string log = """
            Iniciando "ADMIN_BD"."IMP_TABLE_1":
            ORA-39151: La tabla "DEMO_VENTAS"."FACTURA" existe.
            . . "DEMO_VENTAS"."CLIENTE"  142.9 KB  2000 filas importadas
            ORA-31684: El tipo de objeto USER:"DEMO_VENTAS" ya existe
            El trabajo "ADMIN_BD"."IMP_TABLE_1" ha terminado con 2 error(es)
            """;
        Assert.Equal(2, DlgLog.Errores(log));
        Assert.Equal(0, DlgLog.Errores("El trabajo ha terminado correctamente"));
    }

    [OracleFact]
    public async Task Consultas_del_catalogo_y_del_progreso_corren()
    {
        var catalogo = await _bd.ConsultarAsync(SqlRespaldos.Catalogo);
        foreach (var columna in new[] { "ID_RESPALDO", "TIPO", "OBJETOS", "ARCHIVO_DUMP", "ARCHIVO_LOG", "RUTA_SO", "DURACION_SEG", "TAMANO_MB", "ESTADO", "OBSERVACION" })
            Assert.True(catalogo.Columns.Contains(columna), columna);

        Assert.Null(await _bd.EscalarAsync(SqlRespaldos.EstadoJob, Parametro.De("job", "NO_EXISTE_XYZ")));
        Assert.Empty((await _bd.ConsultarAsync(SqlRespaldos.Avance, Parametro.De("job", "NO_EXISTE_XYZ"))).Rows);
        await _bd.ConsultarAsync(SqlRespaldos.JobsColgados);
    }
}

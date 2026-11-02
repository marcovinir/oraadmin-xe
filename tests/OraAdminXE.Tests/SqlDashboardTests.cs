using OraAdminXE.Datos;
using OraAdminXE.Modulos.Dashboard;

namespace OraAdminXE.Tests;

/// <summary>Las consultas de Inicio corren en Oracle y devuelven lo que la pantalla espera (issue #6).</summary>
public class SqlDashboardTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task Instancia_devuelve_version_y_contenedor()
    {
        var t = await _bd.ConsultarAsync(SqlDashboard.Instancia);
        Assert.Single(t.Rows);
        Assert.StartsWith("21.", (string)t.Rows[0]["VERSION_FULL"]);
        Assert.Equal("XEPDB1", t.Rows[0]["CONTENEDOR"]);
    }

    [OracleFact]
    public async Task Sesiones_cuenta_al_menos_la_propia()
    {
        var t = await _bd.ConsultarAsync(SqlDashboard.Sesiones);
        Assert.True(Convert.ToInt32(t.Rows[0]["TOTAL"]) >= 1);
    }

    [OracleFact]
    public async Task UltimoRespaldo_corre_aunque_no_haya_respaldos()
    {
        var t = await _bd.ConsultarAsync(SqlDashboard.UltimoRespaldo);
        Assert.True(t.Rows.Count <= 1);
    }

    [OracleFact]
    public async Task Tablespaces_incluye_USERS_con_porcentaje()
    {
        var t = await _bd.ConsultarAsync(SqlDashboard.Tablespaces);
        var users = t.Select("TABLESPACE_NAME = 'USERS'").Single();
        var pct = Convert.ToDouble(users["PCT_USADO"]);
        Assert.InRange(pct, 0, 100);
    }

    [OracleFact]
    public async Task DatosUsuario_devuelve_gigas()
    {
        var gb = Convert.ToDouble(await _bd.EscalarAsync(SqlDashboard.DatosUsuario));
        Assert.InRange(gb, 0, SqlDashboard.LimiteXeGb);
    }
}

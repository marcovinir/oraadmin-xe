using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Tests;

/// <summary>Pruebas de integración de la capa de datos contra Oracle 21c XE (issue #3).</summary>
public class BaseDatosTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _tabla = "PRUEBA_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        try { await _bd.EjecutarAsync(Modulo.General, "Limpieza de prueba", $"DROP TABLE {_tabla} PURGE"); }
        catch (OracleException) { }
    }

    private Task<object?> UltimaBitacora(string columna) =>
        _bd.EscalarAsync($"SELECT {columna} FROM admin_bd.bitacora WHERE accion = :a ORDER BY id_bitacora DESC FETCH FIRST 1 ROW ONLY",
            new OracleParameter("a", "Prueba " + _tabla));

    [OracleFact]
    public void Conectar_llena_usuario_y_config()
    {
        Assert.Equal("ADMIN_BD", _bd.Usuario);
        Assert.Equal("localhost:1521/XEPDB1", _bd.Config.DataSource);
    }

    [OracleFact]
    public async Task Conectar_con_clave_mala_lanza_ORA_01017()
    {
        var ex = await Assert.ThrowsAsync<OracleException>(() => new BaseDatos().ConectarAsync(new ConfigConexion(), "clave_incorrecta"));
        Assert.Equal(1017, ex.Number);
    }

    [OracleFact]
    public async Task Conectar_sin_rol_DBA_se_rechaza()
    {
        if (OracleFactAttribute.ClaveDemo is not { } claveDemo) return;   // requiere la clave de DEMO_VENTAS
        var config = new ConfigConexion { Usuario = "DEMO_VENTAS" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BaseDatos().ConectarAsync(config, claveDemo));
    }

    [OracleFact]
    public async Task Consultar_devuelve_filas_con_bind()
    {
        var tabla = await _bd.ConsultarAsync("SELECT tablespace_name FROM dba_tablespaces WHERE tablespace_name = :ts",
            new OracleParameter("ts", "USERS"));
        Assert.Single(tabla.Rows);
        Assert.Equal("USERS", tabla.Rows[0]["TABLESPACE_NAME"]);
    }

    [OracleFact]
    public async Task Escalar_devuelve_null_si_no_hay_valor() =>
        Assert.Null(await _bd.EscalarAsync("SELECT NULL FROM dual"));

    [OracleFact]
    public async Task Ejecutar_registra_OK_y_luego_ERROR_con_el_ORA()
    {
        await _bd.EjecutarAsync(Modulo.General, "Prueba " + _tabla, $"CREATE TABLE {_tabla} (id NUMBER);");
        Assert.Equal("OK", await UltimaBitacora("resultado"));

        var ex = await Assert.ThrowsAsync<OracleException>(() =>
            _bd.EjecutarAsync(Modulo.General, "Prueba " + _tabla, $"CREATE TABLE {_tabla} (id NUMBER)"));
        Assert.Equal(955, ex.Number);
        Assert.Equal("ERROR", await UltimaBitacora("resultado"));
        Assert.Contains("ORA-00955", (string?)await UltimaBitacora("mensaje"));
    }

    [OracleFact]
    public async Task Ejecutar_bloque_plsql()
    {
        await _bd.EjecutarAsync(Modulo.General, "Prueba " + _tabla, "BEGIN NULL; END;");
        Assert.Equal("OK", await UltimaBitacora("resultado"));
    }

    [OracleFact]
    public async Task Ejecutar_no_guarda_contrasenas_en_la_bitacora()
    {
        var usuario = "U" + _tabla;
        await _bd.EjecutarAsync(Modulo.Seguridad, "Prueba " + _tabla, $"CREATE USER {usuario} IDENTIFIED BY \"Secreta_123\"");
        try
        {
            var sentencia = (string?)await UltimaBitacora("sentencia");
            Assert.DoesNotContain("Secreta_123", sentencia);
            Assert.Contains("\"********\"", sentencia);
        }
        finally
        {
            await _bd.EjecutarAsync(Modulo.Seguridad, "Limpieza de prueba", $"DROP USER {usuario} CASCADE");
        }
    }

    [OracleFact]
    public async Task EnMismaSesion_permite_leer_el_plan_real()
    {
        var plan = await _bd.EnMismaSesionAsync(async conexion =>
        {
            using (var cmd = new OracleCommand("SELECT /*+ GATHER_PLAN_STATISTICS */ COUNT(*) FROM dual", conexion))
                await cmd.ExecuteScalarAsync();
            using var plan = new OracleCommand("SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST'))", conexion);
            using var lector = await plan.ExecuteReaderAsync();
            var lineas = new List<string>();
            while (await lector.ReadAsync()) lineas.Add(lector.GetString(0));
            return string.Join('\n', lineas);
        });
        Assert.Contains("A-Rows", plan);
    }
}

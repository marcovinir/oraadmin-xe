using OraAdminXE.Modulos.Auditoria;

namespace OraAdminXE.Tests;

public class DdlPoliticasAuditoriaTests
{
    [Fact]
    public void Crear_objeto_repite_ON_por_cada_accion()
    {
        var sql = DdlPoliticasAuditoria.CrearObjeto("pol_demo_dml", "demo_ventas", "factura", ["select", "INSERT", "update", "delete"]);

        Assert.Equal("CREATE AUDIT POLICY POL_DEMO_DML ACTIONS SELECT ON DEMO_VENTAS.FACTURA, INSERT ON DEMO_VENTAS.FACTURA, UPDATE ON DEMO_VENTAS.FACTURA, DELETE ON DEMO_VENTAS.FACTURA", sql);
    }

    [Fact]
    public void Crear_sistema_solo_admite_allowlist()
    {
        Assert.Equal("CREATE AUDIT POLICY POL_DDL ACTIONS CREATE TABLE, DROP TABLE, GRANT", 
            DdlPoliticasAuditoria.CrearSistema("POL_DDL", ["CREATE TABLE", "DROP TABLE", "GRANT"]));
        Assert.Throws<ArgumentException>(() => DdlPoliticasAuditoria.CrearSistema("POL_DDL", ["DROP DATABASE"]));
        Assert.Throws<ArgumentException>(() => DdlPoliticasAuditoria.CrearSistema("POL_DDL", []));
    }

    [Theory]
    [InlineData("AUDIT_DEMO")]
    [InlineData("POL_")]
    [InlineData("ORA_LOGON_FAILURES")]
    [InlineData("POL_X; DROP AUDIT POLICY X")]
    public void Politica_exige_prefijo_y_no_acepta_inyeccion(string politica) =>
        Assert.Throws<ArgumentException>(() => DdlPoliticasAuditoria.CrearSistema(politica, ["LOGON"]));

    [Fact]
    public void Activar_genera_variantes_permitidas()
    {
        Assert.Equal("AUDIT POLICY POL_DEMO", DdlPoliticasAuditoria.Activar("POL_DEMO", null, FiltroResultadoAuditoria.Ambos));
        Assert.Equal("AUDIT POLICY POL_DEMO BY JUAN WHENEVER SUCCESSFUL", DdlPoliticasAuditoria.Activar("POL_DEMO", "juan", FiltroResultadoAuditoria.Exitos));
        Assert.Equal("AUDIT POLICY POL_DEMO BY JUAN WHENEVER NOT SUCCESSFUL", DdlPoliticasAuditoria.Activar("POL_DEMO", "juan", FiltroResultadoAuditoria.Fallos));
    }

    [Fact]
    public void Noaudit_por_usuarios_crea_una_sentencia_por_alcance()
    {
        var sql = DdlPoliticasAuditoria.DesactivarTodas("POL_DEMO",
        [
            new("BY USER", "JUAN", "USER", "YES", "YES"),
            new("BY", "ANA", "USER", "YES", "NO")
        ]);

        Assert.Equal(["NOAUDIT POLICY POL_DEMO BY JUAN", "NOAUDIT POLICY POL_DEMO BY ANA"], sql);
    }

    [Theory]
    [InlineData("BY USER", "ALL USERS", "USER")]
    [InlineData("EXCEPT USER", "JUAN", "USER")]
    public void Noaudit_global_o_except_omite_BY(string opcion, string entidad, string tipo)
    {
        var sql = DdlPoliticasAuditoria.DesactivarTodas("POL_DEMO", [new(opcion, entidad, tipo, "YES", "YES")]);
        Assert.Equal(["NOAUDIT POLICY POL_DEMO"], sql);
    }

    [Fact]
    public void Noaudit_falla_cerrado_para_roles_y_alcances_mezclados()
    {
        Assert.Throws<InvalidOperationException>(() => DdlPoliticasAuditoria.DesactivarTodas("POL_DEMO", [new("BY GRANTED ROLE", "ROL_A", "ROLE", "YES", "YES")]));
        Assert.Throws<InvalidOperationException>(() => DdlPoliticasAuditoria.DesactivarTodas("POL_DEMO",
            [new("BY USER", "JUAN", "USER", "YES", "YES"), new("EXCEPT USER", "ANA", "USER", "YES", "YES")]));
    }

    [Fact]
    public void Eliminar_requiere_proveniencia_local_y_no_oracle()
    {
        Assert.True(DdlPoliticasAuditoria.PuedeEliminar("POL_DEMO", "NO", "NO", "NO", true));
        Assert.False(DdlPoliticasAuditoria.PuedeEliminar("POL_DEMO", "YES", "NO", "NO", true));
        Assert.False(DdlPoliticasAuditoria.PuedeEliminar("POL_DEMO", "NO", "YES", "NO", true));
        Assert.False(DdlPoliticasAuditoria.PuedeEliminar("POL_DEMO", "NO", "NO", "YES", true));
        Assert.False(DdlPoliticasAuditoria.PuedeEliminar("POL_DEMO", "NO", "NO", "NO", false));
        Assert.False(DdlPoliticasAuditoria.PuedeEliminar("ORA_LOGON_FAILURES", "NO", "NO", "NO", true));
    }

    [Fact]
    public void Atajo_logon_usa_fallos_unicamente() =>
        Assert.Equal("AUDIT POLICY ORA_LOGON_FAILURES WHENEVER NOT SUCCESSFUL", DdlPoliticasAuditoria.ActivarFallosLogon());
}

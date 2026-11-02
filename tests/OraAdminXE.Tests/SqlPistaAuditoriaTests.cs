using System.Data;
using OraAdminXE.Modulos.Auditoria;

namespace OraAdminXE.Tests;

public class SqlPistaAuditoriaTests
{
    private static readonly DateTime Desde = new(2026, 10, 27, 8, 0, 0);
    private static readonly DateTime Hasta = new(2026, 10, 28, 8, 0, 0);

    [Fact]
    public void Consulta_usa_binds_limite_y_orden_mas_reciente()
    {
        var consulta = SqlPistaAuditoria.CrearConsulta(new(Desde, Hasta, "ADMIN_BD", "LOGON", "POL_DEMO", true));

        Assert.Contains("event_timestamp >= :desde", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH FIRST 500 ROWS ONLY", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY event_timestamp DESC", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["desde", "hasta", "usuario", "accion", "politica"], consulta.Parametros.Select(p => p.ParameterName));
        Assert.Contains("return_code <> 0", consulta.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filtro_de_politica_compara_nombres_completos_y_no_like()
    {
        var consulta = SqlPistaAuditoria.CrearConsulta(new(Desde, Hasta, null, null, "POL_X", false));

        Assert.Contains("INSTR(',' || REPLACE(unified_audit_policies", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIKE", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("POL_X", consulta.Parametros.Single(p => p.ParameterName == "politica").Value);
    }

    [Theory]
    [InlineData("DROP DATABASE")]
    [InlineData("LOGON; DELETE FROM X")]
    public void Accion_fuera_de_allowlist_falla_cerrado(string accion) =>
        Assert.Throws<ArgumentException>(() => SqlPistaAuditoria.NormalizarFiltro(Desde, Hasta, null, accion, null, false));

    [Theory]
    [InlineData("AUDIT")]
    [InlineData("NOAUDIT")]
    [InlineData("CREATE AUDIT POLICY")]
    [InlineData("DROP AUDIT POLICY")]
    public void Acciones_de_administracion_de_auditoria_se_aceptan_exactamente(string accion)
    {
        var filtro = SqlPistaAuditoria.NormalizarFiltro(Desde, Hasta, null, accion, null, false);
        var consulta = SqlPistaAuditoria.CrearConsulta(filtro);

        Assert.Equal(accion, filtro.Accion);
        Assert.Equal(accion, consulta.Parametros.Single(p => p.ParameterName == "accion").Value);
    }

    [Fact]
    public void Todas_las_acciones_aceptadas_incluyen_las_de_administracion_y_todos_sigue_sin_filtro()
    {
        Assert.Contains("AUDIT", SqlPistaAuditoria.AccionesPermitidas);
        Assert.Contains("NOAUDIT", SqlPistaAuditoria.AccionesPermitidas);
        Assert.Contains("CREATE AUDIT POLICY", SqlPistaAuditoria.AccionesPermitidas);
        Assert.Contains("DROP AUDIT POLICY", SqlPistaAuditoria.AccionesPermitidas);
        Assert.Null(SqlPistaAuditoria.NormalizarFiltro(Desde, Hasta, null, "Todos", null, false).Accion);
    }

    [Fact]
    public void Usuario_y_politica_se_validan_como_identificadores()
    {
        var filtro = SqlPistaAuditoria.NormalizarFiltro(Desde, Hasta, "juan", "Todas", "pol_demo", false);
        Assert.Equal("JUAN", filtro.Usuario);
        Assert.Equal("POL_DEMO", filtro.Politica);
        Assert.Throws<ArgumentException>(() => SqlPistaAuditoria.NormalizarFiltro(Desde, Hasta, "JUAN' OR '1'='1", null, null, false));
    }

    [Fact]
    public void Rango_invertido_falla_antes_de_consultar() =>
        Assert.Throws<ArgumentException>(() => SqlPistaAuditoria.CrearConsulta(new(Hasta, Desde, null, null, null, false)));

    [Theory]
    [InlineData(0, "Correcto")]
    [InlineData(1017, "ORA-01017")]
    [InlineData(28000, "ORA-28000")]
    [InlineData(942, "Fallo de Oracle")]
    public void Interpreta_solo_codigos_conocidos(int codigo, string esperado) =>
        Assert.Contains(esperado, SqlPistaAuditoria.InterpretarReturnCode(codigo));

    [Fact]
    public void Identidad_incompleta_falla_cerrado_y_no_genera_consulta_detalle()
    {
        var tabla = TablaEvento();
        tabla.Rows[0]["STATEMENT_ID"] = DBNull.Value;

        Assert.False(SqlPistaAuditoria.TryCrearIdentidad(tabla.Rows[0], out _));
        Assert.Throws<InvalidOperationException>(() => SqlPistaAuditoria.CrearConsultaDetalle(default));
    }

    [Fact]
    public void Detalle_usa_todos_los_componentes_de_identidad_y_limite_de_clob()
    {
        Assert.True(SqlPistaAuditoria.TryCrearIdentidad(TablaEvento().Rows[0], out var identidad));
        var consulta = SqlPistaAuditoria.CrearConsultaDetalle(identidad);

        Assert.Contains("DBMS_LOB.SUBSTR(sql_text, 4000, 1)", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("event_timestamp_utc = :event_timestamp_utc", consulta.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["event_timestamp_utc", "dbid", "instance_id", "sessionid", "entry_id", "statement_id"], consulta.Parametros.Select(p => p.ParameterName));
    }

    [Fact]
    public void Null_sql_y_fallidos_se_manejan_sin_excepcion()
    {
        Assert.False(SqlPistaAuditoria.EsFallido(DBNull.Value));
        Assert.True(SqlPistaAuditoria.EsFallido(1017));
        Assert.Equal("RETURN_CODE no disponible.", SqlPistaAuditoria.InterpretarReturnCode(DBNull.Value));
    }

    private static DataTable TablaEvento()
    {
        var tabla = new DataTable();
        tabla.Columns.Add("EVENT_TIMESTAMP_UTC", typeof(DateTime));
        tabla.Columns.Add("DBID", typeof(decimal));
        tabla.Columns.Add("INSTANCE_ID", typeof(decimal));
        tabla.Columns.Add("SESSIONID", typeof(decimal));
        tabla.Columns.Add("ENTRY_ID", typeof(decimal));
        tabla.Columns.Add("STATEMENT_ID", typeof(decimal));
        tabla.Rows.Add(Hasta, 1m, 1m, 25m, 77m, 5m);
        return tabla;
    }
}

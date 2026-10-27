using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Tests;

/// <summary>Bloque PL/SQL y validaciones del asistente de respaldos (issue #11), sin base de datos.</summary>
public class ServicioRespaldosTests
{
    private static readonly DirectorioRespaldo Dir = new(1, "DIR_RESPALDOS", @"C:\respaldos_bd");

    [Fact]
    public void Nombre_sugerido_lleva_tipo_y_fecha() =>
        Assert.Equal("EXP_SCHEMA_20261027_153000", ServicioRespaldos.NombreSugerido(TipoRespaldo.Schema, new DateTime(2026, 10, 27, 15, 30, 0)));

    [Fact]
    public void Bloque_por_esquema()
    {
        var bloque = ServicioRespaldos.ArmarBloque(new(TipoRespaldo.Schema, Dir, "exp_schema_1", ["demo_ventas", "admin_bd"], []));
        Assert.Contains("h := DBMS_DATAPUMP.OPEN('EXPORT', 'SCHEMA', NULL, 'EXP_SCHEMA_1');", bloque);
        Assert.Contains("DBMS_DATAPUMP.ADD_FILE(h, 'EXP_SCHEMA_1.DMP', 'DIR_RESPALDOS');", bloque);
        Assert.Contains("DBMS_DATAPUMP.ADD_FILE(h, 'EXP_SCHEMA_1.LOG', 'DIR_RESPALDOS', filetype => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);", bloque);
        Assert.Contains("DBMS_DATAPUMP.METADATA_FILTER(h, 'SCHEMA_EXPR', 'IN (''DEMO_VENTAS'',''ADMIN_BD'')');", bloque);
        Assert.DoesNotContain("NAME_EXPR", bloque);
        Assert.Contains("UTL_FILE.FGETATTR('DIR_RESPALDOS', 'EXP_SCHEMA_1.DMP', existe, tamano, bloque);", bloque);
        Assert.Contains(":estado := estado;", bloque);
        Assert.DoesNotContain("\r", bloque);
    }

    [Fact]
    public void Bloque_por_tabla_filtra_esquema_y_tablas()
    {
        var bloque = ServicioRespaldos.ArmarBloque(new(TipoRespaldo.Table, Dir, "EXP_TABLE_1", ["DEMO_VENTAS"], ["factura", "cliente"]));
        Assert.Contains("OPEN('EXPORT', 'TABLE'", bloque);
        Assert.Contains("'SCHEMA_EXPR', 'IN (''DEMO_VENTAS'')'", bloque);
        Assert.Contains("'NAME_EXPR', 'IN (''FACTURA'',''CLIENTE'')'", bloque);
    }

    [Fact]
    public void Bloque_full_no_lleva_filtros()
    {
        var bloque = ServicioRespaldos.ArmarBloque(new(TipoRespaldo.Full, Dir, "EXP_FULL_1", [], []));
        Assert.Contains("OPEN('EXPORT', 'FULL'", bloque);
        Assert.DoesNotContain("METADATA_FILTER", bloque);
    }

    [Fact]
    public void Objetos_del_catalogo()
    {
        Assert.Equal([("DEMO_VENTAS", (string?)null)],
            ServicioRespaldos.Objetos(new(TipoRespaldo.Schema, Dir, "X", ["demo_ventas"], [])));
        Assert.Equal([("DEMO_VENTAS", (string?)"FACTURA"), ("DEMO_VENTAS", (string?)"CLIENTE")],
            ServicioRespaldos.Objetos(new(TipoRespaldo.Table, Dir, "X", ["DEMO_VENTAS"], ["FACTURA", "CLIENTE"])));
        Assert.Empty(ServicioRespaldos.Objetos(new(TipoRespaldo.Full, Dir, "X", [], [])));
    }

    [Theory]
    [InlineData(TipoRespaldo.Schema, new string[0], new string[0])]          // sin esquemas
    [InlineData(TipoRespaldo.Table, new[] { "DEMO_VENTAS" }, new string[0])] // sin tablas
    [InlineData(TipoRespaldo.Table, new[] { "A", "B" }, new[] { "T" })]      // tablas de dos esquemas
    [InlineData(TipoRespaldo.Schema, new[] { "DEMO'); DROP USER SYS; --" }, new string[0])]
    public void Validar_rechaza_solicitudes_incompletas_o_peligrosas(TipoRespaldo tipo, string[] esquemas, string[] tablas) =>
        Assert.Throws<ArgumentException>(() => ServicioRespaldos.ArmarBloque(new(tipo, Dir, "EXP_X", esquemas, tablas)));

    [Theory]
    [InlineData("")]
    [InlineData("1EXP")]
    [InlineData("EXP.DMP")]
    [InlineData("exp-x")]
    public void Validar_rechaza_nombres_de_archivo_invalidos(string nombre) =>
        Assert.Throws<ArgumentException>(() => ServicioRespaldos.ArmarBloque(new(TipoRespaldo.Full, Dir, nombre, [], [])));
}

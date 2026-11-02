using OraAdminXE.Datos;
using OraAdminXE.Modulos.Recuperacion;
using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Tests;

/// <summary>Bloque de import, validaciones y lectura del log (issue #13), sin base de datos.</summary>
public class RecuperacionTests : IDisposable
{
    private readonly string _carpeta = Directory.CreateTempSubdirectory("oraadmin_").FullName;
    private readonly RespaldoRestaurable _esquema;
    private readonly RespaldoRestaurable _tabla;

    public RecuperacionTests()
    {
        File.WriteAllText(Path.Combine(_carpeta, "EXP_SCHEMA_1.DMP"), "x");
        File.WriteAllText(Path.Combine(_carpeta, "EXP_TABLE_1.DMP"), "x");
        File.WriteAllText(Path.Combine(_carpeta, "EXP_SCHEMA_1.LOG"), """
            . . "DEMO_VENTAS"."DETALLE_FACTURA"             4.084 MB  200000 filas exportadas
            . . "DEMO_VENTAS"."FACTURA"                     1018. KB   50000 filas exportadas
            """);
        _esquema = new(1, TipoRespaldo.Schema, "DIR_RESPALDOS", _carpeta, "EXP_SCHEMA_1.DMP", "EXP_SCHEMA_1.LOG", [("DEMO_VENTAS", null)]);
        _tabla = new(2, TipoRespaldo.Table, "DIR_RESPALDOS", _carpeta, "EXP_TABLE_1.DMP", "EXP_TABLE_1.LOG", [("DEMO_VENTAS", "FACTURA")]);
    }

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    private static readonly DateTime Fecha = new(2026, 10, 27, 16, 0, 0);

    [Fact]
    public void Tabla_desde_respaldo_de_esquema_con_replace()
    {
        var s = new SolicitudRestauracion(_esquema, AlcanceRestauracion.Tablas, ["DEMO_VENTAS"], ["detalle_factura"], AccionExistente.Replace, null, Fecha);
        var bloque = ServicioRecuperacion.ArmarBloque(s);
        Assert.Equal("IMP_TABLE_20261027_160000", s.Job);
        Assert.Contains("h := DBMS_DATAPUMP.OPEN('IMPORT', 'TABLE', NULL, 'IMP_TABLE_20261027_160000');", bloque);
        Assert.Contains("DBMS_DATAPUMP.ADD_FILE(h, 'EXP_SCHEMA_1.DMP', 'DIR_RESPALDOS');", bloque);
        Assert.Contains("DBMS_DATAPUMP.ADD_FILE(h, 'IMP_TABLE_20261027_160000.LOG', 'DIR_RESPALDOS', filetype", bloque);
        Assert.Contains("'NAME_EXPR', 'IN (''DETALLE_FACTURA'')'", bloque);
        Assert.Contains("DBMS_DATAPUMP.SET_PARAMETER(h, 'TABLE_EXISTS_ACTION', 'REPLACE');", bloque);
        Assert.DoesNotContain("METADATA_REMAP", bloque);
        Assert.True(s.Destructiva);
    }

    [Fact]
    public void Esquema_en_otro_esquema_con_remap()
    {
        var s = new SolicitudRestauracion(_esquema, AlcanceRestauracion.Esquemas, ["DEMO_VENTAS"], [], AccionExistente.Skip, "demo_copia", Fecha);
        var bloque = ServicioRecuperacion.ArmarBloque(s);
        Assert.Contains("OPEN('IMPORT', 'SCHEMA'", bloque);
        Assert.Contains("'SCHEMA_EXPR', 'IN (''DEMO_VENTAS'')'", bloque);
        Assert.Contains("DBMS_DATAPUMP.METADATA_REMAP(h, 'REMAP_SCHEMA', 'DEMO_VENTAS', 'DEMO_COPIA');", bloque);
        Assert.False(s.Destructiva);
        Assert.Equal([("DEMO_COPIA", "DETALLE_FACTURA"), ("DEMO_COPIA", "FACTURA")], ServicioRecuperacion.TablasAfectadas(s));
    }

    [Fact]
    public void Todo_el_respaldo_usa_el_modo_del_export_sin_filtros()
    {
        var s = new SolicitudRestauracion(_tabla, AlcanceRestauracion.Todo, [], [], AccionExistente.Truncate, null, Fecha);
        var bloque = ServicioRecuperacion.ArmarBloque(s);
        Assert.Contains("OPEN('IMPORT', 'TABLE'", bloque);
        Assert.DoesNotContain("METADATA_FILTER", bloque);
        Assert.Equal([("DEMO_VENTAS", (string?)"FACTURA")], ServicioRecuperacion.Objetos(s));
        Assert.True(s.Destructiva);
    }

    [Fact]
    public void No_se_puede_elegir_un_respaldo_cuyo_archivo_se_borro()
    {
        File.Delete(Path.Combine(_carpeta, "EXP_SCHEMA_1.DMP"));
        Assert.False(_esquema.ExisteEnDisco);
        var s = new SolicitudRestauracion(_esquema, AlcanceRestauracion.Todo, [], [], AccionExistente.Skip, null, Fecha);
        Assert.Throws<ArgumentException>(() => ServicioRecuperacion.ArmarBloque(s));
    }

    [Fact]
    public void Validaciones()
    {
        void Falla(SolicitudRestauracion s) => Assert.Throws<ArgumentException>(() => ServicioRecuperacion.ArmarBloque(s));
        Falla(new(_esquema, AlcanceRestauracion.Esquemas, [], [], AccionExistente.Skip, null, Fecha));                       // sin esquemas
        Falla(new(_tabla, AlcanceRestauracion.Esquemas, ["DEMO_VENTAS"], [], AccionExistente.Skip, null, Fecha));            // esquema desde un respaldo de tabla
        Falla(new(_esquema, AlcanceRestauracion.Tablas, ["DEMO_VENTAS"], [], AccionExistente.Skip, null, Fecha));            // sin tablas
        Falla(new(_esquema, AlcanceRestauracion.Esquemas, ["A", "B"], [], AccionExistente.Skip, "COPIA", Fecha));            // remap con dos esquemas
        Falla(new(_esquema, AlcanceRestauracion.Esquemas, ["DEMO_VENTAS"], [], AccionExistente.Skip, "DEMO_VENTAS", Fecha)); // remap al mismo
        Falla(new(_esquema, AlcanceRestauracion.Tablas, ["DEMO_VENTAS"], ["X'); DROP"], AccionExistente.Skip, null, Fecha));
    }

    [Fact]
    public void Esquemas_del_respaldo_full_salen_del_log_sin_los_de_Oracle()
    {
        File.WriteAllText(Path.Combine(_carpeta, "EXP_FULL_1.LOG"), """
            . . "SYS"."KU$_USER_MAPPING_VIEW"               6.109 KB      39 filas exportadas
            . . "AUDSYS"."AUD$UNIFIED":"SYS_P281"           60.24 KB      18 filas exportadas
            . . "AUDSYS"."AUD$UNIFIED":"SYS_P1668"          80.32 KB      49 filas exportadas
            . . "DEMO_VENTAS"."FACTURA"                     1018. KB   50000 filas exportadas
            """);
        var full = new RespaldoRestaurable(3, TipoRespaldo.Full, "DIR_RESPALDOS", _carpeta, "EXP_FULL_1.DMP", "EXP_FULL_1.LOG", []);
        Assert.Equal(["DEMO_VENTAS"], full.Esquemas(["SYS", "AUDSYS"]));

        var tablas = LogDataPump.TablasDeArchivo(full.RutaLog);
        Assert.Equal(3, tablas.Count);
        Assert.Contains(("AUDSYS", "AUD$UNIFIED", 67L), tablas);   // las particiones se suman
    }
}

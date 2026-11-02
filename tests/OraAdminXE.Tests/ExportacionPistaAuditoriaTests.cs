using System.Data;
using OraAdminXE.Modulos.Auditoria;
using OraAdminXE.UI;

namespace OraAdminXE.Tests;

public class ExportacionPistaAuditoriaTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData(" \t@SUM(A1:A2)")]
    [InlineData("\u0001-10")]
    [InlineData("\u200B+cmd")]
    [InlineData("\uFF1D1+1")]
    [InlineData("\u2212cmd")]
    public void Sanitiza_prefijos_de_formula_incluso_con_espacios_controles_y_unicode(string texto) =>
        Assert.StartsWith("'", ExportacionPistaAuditoria.Sanitizar(texto));

    [Fact]
    public void Preparar_exporta_solo_columnas_visibles_y_no_modifica_origen()
    {
        var origen = Tabla();
        origen.Rows[0]["SQL_TEXT_PREVIEW"] = "=HYPERLINK(\"https://ejemplo\")";
        origen.Columns.Add("SQL_BINDS", typeof(string));
        origen.Rows[0]["SQL_BINDS"] = "secreto";

        var exportacion = ExportacionPistaAuditoria.Preparar(origen);

        Assert.Equal(10, exportacion.Columns.Count);
        Assert.DoesNotContain(exportacion.Columns.Cast<DataColumn>(), c => c.ColumnName == "SQL_BINDS");
        Assert.StartsWith("'", (string)exportacion.Rows[0]["SQL (vista previa)"]);
        Assert.Equal("=HYPERLINK(\"https://ejemplo\")", origen.Rows[0]["SQL_TEXT_PREVIEW"]);
    }

    [Fact]
    public void Csv_serializa_separador_comillas_y_saltos_de_linea_despues_de_sanitizar()
    {
        var tabla = Tabla();
        tabla.Rows[0]["OBJECT_NAME"] = "a,b;\"c\"\r\nlínea dos";

        var csv = Csv.Generar(ExportacionPistaAuditoria.Preparar(tabla));

        Assert.Contains("\"a,b;\"\"c\"\"\r\nlínea dos\"", csv);
        Assert.StartsWith("Fecha (BD);Usuario BD;", csv);
    }

    [Fact]
    public void Nulos_y_fechas_se_exportan_con_encabezados_estables()
    {
        var tabla = Tabla();
        tabla.Rows[0]["OS_USERNAME"] = DBNull.Value;

        var exportacion = ExportacionPistaAuditoria.Preparar(tabla);

        Assert.Equal("", exportacion.Rows[0]["Usuario SO"]);
        Assert.Equal("2026-10-28 08:00:00", exportacion.Rows[0]["Fecha (BD)"]);
    }

    [Theory]
    [InlineData("SELECT 1\0", "SELECT 1")]
    [InlineData("SE\0LECT 1", "SELECT 1")]
    [InlineData("\0=1+1", "'=1+1")]
    [InlineData("\u0001+1+1", "'+1+1")]
    [InlineData("\u0002-1", "'-1")]
    [InlineData("\u0003@SUM(A1:A2)", "'@SUM(A1:A2)")]
    public void Elimina_controles_invalidos_antes_de_neutralizar_formulas(string entrada, string esperado) =>
        Assert.Equal(esperado, ExportacionPistaAuditoria.Sanitizar(entrada));

    [Fact]
    public void Conserva_tab_cr_lf_legitimos_y_no_emite_nul_en_csv()
    {
        var tabla = Tabla();
        tabla.Rows[0]["SQL_TEXT_PREVIEW"] = "SELECT\tcol\r\nFROM tabla\0";

        var exportacion = ExportacionPistaAuditoria.Preparar(tabla);
        var csv = Csv.Generar(exportacion);

        Assert.Equal("SELECT\tcol\r\nFROM tabla", exportacion.Rows[0]["SQL (vista previa)"]);
        Assert.DoesNotContain('\0', csv);
    }

    [Fact]
    public void Exporta_exactamente_una_fila_csv_por_evento()
    {
        var tabla = Tabla();
        tabla.Rows.Add(new DateTime(2026, 10, 28, 9, 0, 0), "ANA", "win", "host", "AUDIT", "SYS", "X", "0", "POL_X", "AUDIT POLICY POL_X");
        tabla.Rows.Add(new DateTime(2026, 10, 28, 10, 0, 0), "LUIS", "win", "host", "NOAUDIT", "SYS", "X", "0", "POL_X", "NOAUDIT POLICY POL_X");

        var exportacion = ExportacionPistaAuditoria.Preparar(tabla);
        var csv = Csv.Generar(exportacion);

        Assert.Equal(tabla.Rows.Count, exportacion.Rows.Count);
        Assert.Equal(tabla.Rows.Count + 1, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    private static DataTable Tabla()
    {
        var tabla = new DataTable();
        foreach (var nombre in new[] { "EVENT_TIMESTAMP", "DBUSERNAME", "OS_USERNAME", "USERHOST", "ACTION_NAME", "OBJECT_SCHEMA", "OBJECT_NAME", "RETURN_CODE", "UNIFIED_AUDIT_POLICIES", "SQL_TEXT_PREVIEW" })
            tabla.Columns.Add(nombre, nombre is "EVENT_TIMESTAMP" ? typeof(DateTime) : typeof(string));
        tabla.Rows.Add(new DateTime(2026, 10, 28, 8, 0, 0), "JUAN", "win", "host", "UPDATE", "DEMO", "FACTURA", "0", "POL_DEMO", "UPDATE DEMO.FACTURA");
        return tabla;
    }
}

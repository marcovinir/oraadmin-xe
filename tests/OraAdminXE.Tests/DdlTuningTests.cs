using OraAdminXE.Modulos.Tuning;

namespace OraAdminXE.Tests;

public class DdlTuningTests
{
    [Fact]
    public void CrearIndice_arma_el_DDL_con_las_columnas_en_orden() =>
        Assert.Equal(
            "CREATE INDEX DEMO_VENTAS.IX_DET_PRODUCTO ON DEMO_VENTAS.DETALLE_FACTURA(ID_PRODUCTO)",
            DdlTuning.CrearIndice("demo_ventas", "ix_det_producto", "detalle_factura", ["id_producto"], unico: false));

    [Fact]
    public void CrearIndice_unique_y_varias_columnas() =>
        Assert.Equal(
            "CREATE UNIQUE INDEX S.IX ON S.T(B, A)",
            DdlTuning.CrearIndice("S", "IX", "T", ["B", "A"], unico: true));

    [Fact]
    public void CrearIndice_exige_columnas() =>
        Assert.Throws<ArgumentException>(() => DdlTuning.CrearIndice("S", "IX", "T", [], false));

    [Fact]
    public void CrearIndice_rechaza_columnas_repetidas() =>
        Assert.Throws<ArgumentException>(() => DdlTuning.CrearIndice("S", "IX", "T", ["A", "a"], false));

    [Theory]
    [InlineData("IX; DROP TABLE T")]
    [InlineData("1IX")]
    [InlineData("IX X")]
    [InlineData("")]
    public void CrearIndice_rechaza_nombres_peligrosos(string nombre) =>
        Assert.Throws<ArgumentException>(() => DdlTuning.CrearIndice("S", nombre, "T", ["A"], false));

    [Fact]
    public void Reconstruir_y_eliminar()
    {
        Assert.Equal("ALTER INDEX S.IX REBUILD", DdlTuning.ReconstruirIndice("s", "ix"));
        Assert.Equal("DROP INDEX S.IX", DdlTuning.EliminarIndice("s", "ix"));
        Assert.Equal("ALTER INDEX S.\"Foo\" REBUILD", DdlTuning.ReconstruirIndice("s", "\"Foo\""));
        Assert.Equal("DROP INDEX S.\"Foo\"", DdlTuning.EliminarIndice("s", "\"Foo\""));
        Assert.Equal("ALTER INDEX S.\"MY INDEX\" REBUILD", DdlTuning.ReconstruirIndice("s", "\"MY INDEX\""));
        Assert.Throws<ArgumentException>(() => DdlTuning.EliminarIndice("S", "IX; --"));
        Assert.Throws<ArgumentException>(() => DdlTuning.EliminarIndice("S", "\"IX\" ; DROP TABLE T; --\""));
    }

    [Fact]
    public void NombreSugerido_es_IX_tabla_columnas() =>
        Assert.Equal("IX_DETALLE_FACTURA_ID_PRODUCTO", DdlTuning.NombreSugerido("detalle_factura", ["id_producto"]));

    [Fact]
    public void NombreSugerido_se_recorta_a_128() =>
        Assert.Equal(128, DdlTuning.NombreSugerido(new string('T', 100), [new string('C', 100)]).Length);

    [Fact]
    public void RecolectarTabla_usa_bind_y_muestra_los_valores()
    {
        var s = DdlTuning.RecolectarTabla("DEMO_VENTAS", "DETALLE_FACTURA");
        Assert.Equal("BEGIN DBMS_STATS.GATHER_TABLE_STATS(:e, :t, cascade => TRUE); END;", s.Sql);
        Assert.Equal("BEGIN DBMS_STATS.GATHER_TABLE_STATS('DEMO_VENTAS', 'DETALLE_FACTURA', cascade => TRUE); END;", s.Visible);
        Assert.Equal(["e", "t"], s.Parametros.Select(p => p.ParameterName));
    }

    [Fact]
    public void Estadisticas_de_esquema_y_borrado()
    {
        Assert.Equal("BEGIN DBMS_STATS.GATHER_SCHEMA_STATS(:e); END;", DdlTuning.RecolectarEsquema("S").Sql);
        Assert.Equal("BEGIN DBMS_STATS.DELETE_TABLE_STATS('S', 'T'); END;", DdlTuning.BorrarTabla("S", "T").Visible);
    }

    [Fact]
    public void Visible_escapa_las_comillas() =>
        Assert.Contains("'O''X'", DdlTuning.BorrarTabla("S", "O'X").Visible);
}

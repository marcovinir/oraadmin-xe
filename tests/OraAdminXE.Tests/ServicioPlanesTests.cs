using OraAdminXE.Modulos.Tuning;

namespace OraAdminXE.Tests;

public class ServicioPlanesTests
{
    [Theory]
    [InlineData("SELECT 1 FROM dual", "SELECT 1 FROM dual")]
    [InlineData("  select * from t;  ", "select * from t")]
    [InlineData("WITH x AS (SELECT 1 a FROM dual) SELECT a FROM x", "WITH x AS (SELECT 1 a FROM dual) SELECT a FROM x")]
    public void Validar_acepta_consultas(string entrada, string esperado) =>
        Assert.Equal(esperado, ServicioPlanes.Validar(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("DELETE FROM t")]
    [InlineData("DROP TABLE t")]
    [InlineData("SELECTED_ROWS")]
    public void Validar_rechaza_lo_que_no_es_consulta(string? entrada) =>
        Assert.Throws<ArgumentException>(() => ServicioPlanes.Validar(entrada));

    [Fact]
    public void ConHint_lo_inserta_despues_del_primer_select() =>
        Assert.Equal("SELECT /*+ GATHER_PLAN_STATISTICS */ * FROM t", ServicioPlanes.ConHint("SELECT * FROM t"));

    [Fact]
    public void ConHint_en_un_WITH_va_en_el_select_principal_no_en_la_subconsulta() =>
        Assert.Equal(
            "WITH x AS (SELECT 1 a FROM dual) SELECT /*+ GATHER_PLAN_STATISTICS */ a FROM x",
            ServicioPlanes.ConHint("WITH x AS (SELECT 1 a FROM dual) SELECT a FROM x"));

    [Fact]
    public void ConHint_amplia_un_hint_existente() =>
        Assert.Equal(
            "SELECT /*+ GATHER_PLAN_STATISTICS FULL(t) */ * FROM t",
            ServicioPlanes.ConHint("SELECT /*+ FULL(t) */ * FROM t"));

    [Fact]
    public void ConHint_ignora_select_dentro_de_cadenas_y_comentarios() =>
        Assert.Equal(
            "WITH x AS (SELECT 'SELECT' a FROM dual) -- select\nSELECT /*+ GATHER_PLAN_STATISTICS */ a FROM x",
            ServicioPlanes.ConHint("WITH x AS (SELECT 'SELECT' a FROM dual) -- select\nSELECT a FROM x"));

    [Fact]
    public void ConHint_solo_mira_dentro_del_comentario_del_hint() =>
        Assert.Equal(
            "SELECT /*+ GATHER_PLAN_STATISTICS FULL(t) */ 'GATHER_PLAN_STATISTICS' FROM t",
            ServicioPlanes.ConHint("SELECT /*+ FULL(t) */ 'GATHER_PLAN_STATISTICS' FROM t"));

    [Fact]
    public void ConHint_no_confunde_NO_GATHER_PLAN_STATISTICS_con_el_hint() =>
        Assert.Equal(
            "SELECT /*+ GATHER_PLAN_STATISTICS NO_GATHER_PLAN_STATISTICS */ * FROM t",
            ServicioPlanes.ConHint("SELECT /*+ NO_GATHER_PLAN_STATISTICS */ * FROM t"));

    [Fact]
    public void ConHint_con_hint_anidado_lo_agrega_igual() =>
        Assert.Equal(
            "SELECT /*+ GATHER_PLAN_STATISTICS LEADING((t1 t2) GATHER_PLAN_STATISTICS) */ * FROM t1, t2",
            ServicioPlanes.ConHint("SELECT /*+ LEADING((t1 t2) GATHER_PLAN_STATISTICS) */ * FROM t1, t2"));

    [Fact]
    public void ConHint_ignora_el_nombre_dentro_de_los_argumentos_de_otro_hint() =>
        Assert.Equal(
            "SELECT /*+ GATHER_PLAN_STATISTICS FULL(GATHER_PLAN_STATISTICS) */ * FROM t",
            ServicioPlanes.ConHint("SELECT /*+ FULL(GATHER_PLAN_STATISTICS) */ * FROM t"));

    private const string PlanCompleto = """
        Plan hash value: 123

        ---------------------------------------------------------------------------------------
        | Id  | Operation         | Name            | Rows  | Bytes | Cost (%CPU)| Time     |
        ---------------------------------------------------------------------------------------
        |   0 | SELECT STATEMENT  |                 |   404 | 12120 |   173   (1)| 00:00:01 |
        |*  1 |  TABLE ACCESS FULL| DETALLE_FACTURA |   404 | 12120 |   173   (1)| 00:00:01 |
        ---------------------------------------------------------------------------------------
        """;

    [Fact]
    public void OperacionPrincipal_encuentra_el_acceso_a_la_tabla() =>
        Assert.Equal("TABLE ACCESS FULL · DETALLE_FACTURA", ServicioPlanes.OperacionPrincipal(PlanCompleto));

    [Fact]
    public void OperacionPrincipal_es_null_si_no_hay_acceso_a_datos() =>
        Assert.Null(ServicioPlanes.OperacionPrincipal("| 0 | SELECT STATEMENT | | 1 |\n| 1 | FAST DUAL | | 1 |"));

    [Fact]
    public void LineaOperacionPrincipal_devuelve_la_linea_a_resaltar() =>
        Assert.Contains("TABLE ACCESS FULL", ServicioPlanes.LineaOperacionPrincipal(PlanCompleto));

    private static ResultadoPlan ConCosto(decimal? costo) => new("", costo, null, null, null, null);

    [Fact]
    public void CompararCostos_baja_de_173_a_107() =>
        Assert.Equal(new ResumenCosto("173 → 107", "−38 %", true, false), ServicioPlanes.CompararCostos(ConCosto(173), ConCosto(107)));

    [Fact]
    public void CompararCostos_sube() =>
        Assert.Equal(new ResumenCosto("100 → 150", "+50 %", false, true), ServicioPlanes.CompararCostos(ConCosto(100), ConCosto(150)));

    [Fact]
    public void CompararCostos_sin_cambio() =>
        Assert.Equal(new ResumenCosto("50 → 50", "sin cambio", false, false), ServicioPlanes.CompararCostos(ConCosto(50), ConCosto(50)));

    [Fact]
    public void CompararCostos_sin_costo_es_null() =>
        Assert.Null(ServicioPlanes.CompararCostos(ConCosto(null), ConCosto(10)));
}

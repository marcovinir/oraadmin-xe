namespace OraAdminXE.Modulos.Tuning;

/// <summary>Consultas de la pestaña Plan de ejecución (issue #21, probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlTuning
{
    /// <summary>
    /// Consulta de la demo precargada en el editor. Sin índice: TABLE ACCESS FULL, costo 173, 404 filas.
    /// No usar COUNT(*): Oracle lo resuelve con el índice de la PK (INDEX FAST FULL SCAN).
    /// </summary>
    public const string ConsultaDemo = "SELECT * FROM demo_ventas.detalle_factura WHERE id_producto = 25";

    /// <summary>
    /// Llena PLAN_TABLE. El STATEMENT_ID debe ser un literal (no admite bind); lo genera
    /// <see cref="ServicioPlanes"/> con un GUID corto, así que no hay inyección posible.
    /// </summary>
    public static string ExplicarPlan(string statementId, string consulta) =>
        $"EXPLAIN PLAN SET STATEMENT_ID = '{statementId}' FOR {consulta}";

    /// <summary>Texto del plan estimado, ya formateado por DBMS_XPLAN.</summary>
    public const string PlanEstimado =
        "SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY('PLAN_TABLE', :id, 'TYPICAL'))";

    /// <summary>Costo total y filas estimadas del plan (fila 0 = operación raíz).</summary>
    public const string CostoDelPlan =
        "SELECT cost, cardinality FROM plan_table WHERE statement_id = :id AND id = 0";

    public const string LimpiarPlan = "DELETE FROM plan_table WHERE statement_id = :id";

    /// <summary>Plan de la última sentencia ejecutada EN ESTA SESIÓN (con A-Rows y A-Time).</summary>
    public const string PlanReal =
        "SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST'))";

    /// <summary>Top 10 de v$sqlarea por tiempo. «Analizar» pide el texto completo por SQL_ID (<see cref="TextoCompleto"/>).</summary>
    public const string TopSql = """
        SELECT sql_id, executions, ROUND(elapsed_time / 1e6, 2) AS seg, buffer_gets, disk_reads,
               SUBSTR(sql_text, 1, 100) AS texto
        FROM v$sqlarea
        ORDER BY elapsed_time DESC
        FETCH FIRST 10 ROWS ONLY
        """;

    /// <summary>Texto completo de una sentencia (SQL_TEXT solo trae los primeros 1000 caracteres).</summary>
    public const string TextoCompleto = "SELECT sql_fulltext FROM v$sqlarea WHERE sql_id = :id FETCH FIRST 1 ROWS ONLY";
}

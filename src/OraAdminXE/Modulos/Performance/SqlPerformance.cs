namespace OraAdminXE.Modulos.Performance;

/// <summary>Consultas de Performance: Sesiones, bloqueos, matar sesión y métricas en vivo (issues #23, #24).</summary>
public static class SqlPerformance
{
    /// <summary>
    /// Sesiones de usuario (excluye las del sistema). Marca todas las sesiones de la propia app
    /// (por SID actual o por pool de conexiones con mismo usuario, máquina y programa OraAdminXE/dotnet).
    /// Ordena los bloqueados primero.
    /// </summary>
    public const string Sesiones = """
        SELECT s.sid, s.serial#, s.username, s.status, s.machine, s.program, s.sql_id,
               CASE WHEN s.blocking_session_status = 'VALID' THEN s.blocking_session END AS blocking_session,
               s.event, s.seconds_in_wait, s.logon_time,
               CASE WHEN s.sid = SYS_CONTEXT('USERENV', 'SID')
                    OR (s.username = SYS_CONTEXT('USERENV', 'SESSION_USER')
                        AND s.machine = SYS_CONTEXT('USERENV', 'HOST')
                        AND (s.program IS NULL
                             OR LOWER(s.program) LIKE '%oraadminxe%'
                             OR LOWER(s.program) LIKE '%dotnet%'))
                    THEN 'SI' END AS es_la_app
        FROM v$session s
        WHERE s.type = 'USER'
        ORDER BY s.blocking_session NULLS LAST, s.sid
        """;

    /// <summary>Objetos con bloqueos activos registrados en v$locked_object.</summary>
    public const string ObjetosBloqueados = """
        SELECT lo.session_id AS sid, o.owner, o.object_name,
               DECODE(lo.locked_mode,
                      2, 'ROW SHARE',
                      3, 'ROW EXCLUSIVE',
                      4, 'SHARE',
                      5, 'SHARE ROW EXCL',
                      6, 'EXCLUSIVE',
                      TO_CHAR(lo.locked_mode)) AS modo
        FROM v$locked_object lo
        JOIN dba_objects o ON o.object_id = lo.object_id
        """;

    /// <summary>Texto completo de la consulta activa de una sesión.</summary>
    public const string SqlDeSesion =
        "SELECT sql_fulltext FROM v$sql WHERE sql_id = :id AND ROWNUM = 1";

    /// <summary>Valor actual de las métricas del catálogo en v$con_sysmetric (intervalo 60 s, issue #24).</summary>
    public const string MetricasActuales = """
        SELECT m.id_metrica, m.nombre, m.unidad, ROUND(s.value, 2) AS valor
        FROM admin_bd.metrica m
        JOIN v$con_sysmetric s ON s.metric_name = m.nombre_oracle AND s.group_id = 18
        ORDER BY m.id_metrica
        """;

    /// <summary>Inserta una muestra en SNAPSHOT con bind de salida para el ID generado (issue #24).</summary>
    public const string InsertarSnapshot =
        "INSERT INTO admin_bd.snapshot (fecha) VALUES (SYSTIMESTAMP) RETURNING id_snapshot INTO :id";

    /// <summary>Inserta los valores de las métricas para una muestra dada (issue #24).</summary>
    public const string InsertarValoresMetricas = """
        INSERT INTO admin_bd.valor_metrica (id_snapshot, id_metrica, valor)
        SELECT :id, m.id_metrica, s.value
        FROM admin_bd.metrica m
        JOIN v$con_sysmetric s ON s.metric_name = m.nombre_oracle AND s.group_id = 18
        """;

    /// <summary>Historial de muestras de una métrica en la última hora (issue #24).</summary>
    public const string HistorialMetrica = """
        SELECT s.fecha, v.valor
        FROM admin_bd.valor_metrica v
        JOIN admin_bd.snapshot s ON s.id_snapshot = v.id_snapshot
        WHERE v.id_metrica = :m AND s.fecha > SYSTIMESTAMP - INTERVAL '1' HOUR
        ORDER BY s.fecha
        """;

    /// <summary>Borra todo el historial de snapshots (en cascada borra valor_metrica, issue #24).</summary>
    public const string BorrarHistorial = "DELETE FROM admin_bd.snapshot";
}

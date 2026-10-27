namespace OraAdminXE.Modulos.Respaldos;

/// <summary>Consultas del módulo de Respaldos (probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlRespaldos
{
    public const string Esquemas = "SELECT username FROM dba_users WHERE oracle_maintained = 'N' ORDER BY username";

    public const string TablasDe = "SELECT table_name FROM dba_tables WHERE owner = :e ORDER BY table_name";

    public const string Directorios = """
        SELECT id_directorio, nombre_oracle, ruta_so
        FROM admin_bd.directorio WHERE activo = 'S' ORDER BY nombre_oracle
        """;

    /// <summary>¿El nombre ya lo usa un respaldo del catálogo o un job de Data Pump?</summary>
    public const string NombreOcupado = """
        SELECT (SELECT COUNT(*) FROM admin_bd.respaldo WHERE nombre_job = :n)
             + (SELECT COUNT(*) FROM dba_datapump_jobs WHERE owner_name = USER AND job_name = :n)
        FROM dual
        """;

    public const string InsertarRespaldo = """
        INSERT INTO admin_bd.respaldo (id_tipo, id_directorio, nombre_job, archivo_dump, archivo_log, estado, usuario_bd, observacion)
        VALUES ((SELECT id_tipo FROM admin_bd.tipo_respaldo WHERE codigo = :tipo), :dir, :job, :dmp, :log, 'EN_PROCESO', USER, :obs)
        RETURNING id_respaldo INTO :id
        """;

    public const string InsertarObjeto = """
        INSERT INTO admin_bd.respaldo_objeto (id_respaldo, esquema, tabla) VALUES (:id, :esquema, :tabla)
        """;

    public const string Terminar = """
        UPDATE admin_bd.respaldo
        SET estado = :estado, fecha_fin = SYSDATE, tamano_bytes = :tamano
        WHERE id_respaldo = :id
        """;

    /// <summary>Catálogo con los objetos de cada respaldo en una sola columna (DEMO_VENTAS, DEMO_VENTAS.FACTURA…).</summary>
    public const string Catalogo = """
        SELECT v.*,
               (SELECT LISTAGG(NVL2(o.tabla, o.esquema || '.' || o.tabla, o.esquema), ', ')
                       WITHIN GROUP (ORDER BY o.esquema, o.tabla)
                FROM admin_bd.respaldo_objeto o WHERE o.id_respaldo = v.id_respaldo) AS objetos
        FROM admin_bd.v_respaldos v
        ORDER BY v.fecha_inicio DESC, v.id_respaldo DESC
        """;

    /// <summary>Estado del job mientras corre (lo consulta otra sesión del pool).</summary>
    public const string EstadoJob = "SELECT state FROM dba_datapump_jobs WHERE owner_name = USER AND job_name = :job";

    /// <summary>Avance del job; si TOTALWORK es 0 el porcentaje queda NULL.</summary>
    public const string Avance = """
        SELECT sofar, totalwork, ROUND(sofar / NULLIF(totalwork, 0) * 100) AS pct
        FROM v$session_longops
        WHERE opname = :job AND sofar <> totalwork
        ORDER BY start_time DESC FETCH FIRST 1 ROW ONLY
        """;

    /// <summary>
    /// Jobs que quedaron a medias: NOT RUNNING, o DEFINING sin ninguna sesión (un bloque que falló antes de START_JOB).
    /// Su tabla maestra se queda en el esquema hasta que se borra.
    /// </summary>
    public const string JobsColgados = """
        SELECT job_name, operation, job_mode, state, attached_sessions
        FROM dba_datapump_jobs
        WHERE owner_name = USER
          AND (state = 'NOT RUNNING' OR (state = 'DEFINING' AND attached_sessions = 0))
        ORDER BY job_name
        """;

    /// <summary>Respaldos que quedaron EN_PROCESO pero cuyo job ya no existe (la app se cerró a mitad del respaldo).</summary>
    public const string MarcarInterrumpidos = """
        UPDATE admin_bd.respaldo r
        SET estado = 'ERROR', fecha_fin = SYSDATE,
            observacion = SUBSTR(NVL2(observacion, observacion || ' | ', '') || 'Interrumpido: el job ya no existe', 1, 500)
        WHERE estado = 'EN_PROCESO'
          AND NOT EXISTS (SELECT 1 FROM dba_datapump_jobs j WHERE j.owner_name = r.usuario_bd AND j.job_name = r.nombre_job)
        """;

    /// <summary>Marca el respaldo con error y agrega el mensaje de Oracle a la observación (máx. 500).</summary>
    public const string MarcarError = """
        UPDATE admin_bd.respaldo
        SET estado = 'ERROR', fecha_fin = SYSDATE,
            observacion = SUBSTR(NVL2(observacion, observacion || ' | ', '') || :mensaje, 1, 500)
        WHERE id_respaldo = :id
        """;
}

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>Consultas de solo lectura para la pantalla de tablespaces en XEPDB1.</summary>
public static class SqlTablespaces
{
    /// <summary>
    /// Uso actual de tablespaces permanentes, UNDO y temporales. Para TEMP se consulta
    /// V$TEMP_SPACE_HEADER: DBA_FREE_SPACE no describe el espacio temporal en uso.
    /// </summary>
    public const string Uso = """
        WITH datafiles AS (
            SELECT tablespace_name,
                   SUM(bytes) AS total_bytes,
                   CASE
                       WHEN MIN(autoextensible) = 'YES' AND MAX(autoextensible) = 'YES' THEN 'YES'
                       WHEN MIN(autoextensible) = 'NO'  AND MAX(autoextensible) = 'NO'  THEN 'NO'
                       ELSE 'MIXED'
                   END AS autoextend
            FROM dba_data_files
            GROUP BY tablespace_name
        ),
        libre_data AS (
            SELECT tablespace_name, SUM(bytes) AS libre_bytes
            FROM dba_free_space
            GROUP BY tablespace_name
        ),
        tempfiles AS (
            SELECT tablespace_name,
                   SUM(bytes) AS total_bytes,
                   CASE
                       WHEN MIN(autoextensible) = 'YES' AND MAX(autoextensible) = 'YES' THEN 'YES'
                       WHEN MIN(autoextensible) = 'NO'  AND MAX(autoextensible) = 'NO'  THEN 'NO'
                       ELSE 'MIXED'
                   END AS autoextend
            FROM dba_temp_files
            GROUP BY tablespace_name
        ),
        uso_temp AS (
            SELECT tablespace_name, SUM(bytes_used) AS usado_bytes
            FROM v$temp_space_header
            GROUP BY tablespace_name
        ),
        uso AS (
            SELECT ts.tablespace_name,
                   ts.contents,
                   ts.status,
                   df.total_bytes,
                   df.total_bytes - NVL(ld.libre_bytes, 0) AS usado_bytes,
                   NVL(ld.libre_bytes, 0) AS libre_bytes,
                   df.autoextend
            FROM dba_tablespaces ts
            JOIN datafiles df ON df.tablespace_name = ts.tablespace_name
            LEFT JOIN libre_data ld ON ld.tablespace_name = ts.tablespace_name
            WHERE ts.contents IN ('PERMANENT', 'UNDO')

            UNION ALL

            SELECT ts.tablespace_name,
                   ts.contents,
                   ts.status,
                   tf.total_bytes,
                   LEAST(tf.total_bytes, GREATEST(0, NVL(ut.usado_bytes, 0))) AS usado_bytes,
                   tf.total_bytes - LEAST(tf.total_bytes, GREATEST(0, NVL(ut.usado_bytes, 0))) AS libre_bytes,
                   tf.autoextend
            FROM dba_tablespaces ts
            JOIN tempfiles tf ON tf.tablespace_name = ts.tablespace_name
            LEFT JOIN uso_temp ut ON ut.tablespace_name = ts.tablespace_name
            WHERE ts.contents = 'TEMPORARY'
        )
        SELECT tablespace_name,
               contents,
               status,
               ROUND(total_bytes / 1048576, 1) AS total_mb,
               ROUND(usado_bytes / 1048576, 1) AS usado_mb,
               ROUND(libre_bytes / 1048576, 1) AS libre_mb,
               ROUND(usado_bytes / NULLIF(total_bytes, 0) * 100, 1) AS pct_usado,
               autoextend
        FROM uso
        ORDER BY pct_usado DESC NULLS LAST, tablespace_name
        """;

    /// <summary>
    /// Datafiles o tempfiles de un tablespace. Las uniones con el espacio libre/ocupado
    /// usan TABLESPACE_NAME y FILE_ID para no cruzar accidentalmente archivos distintos.
    /// </summary>
    public const string Archivos = """
        WITH libre_data AS (
            SELECT tablespace_name, file_id, SUM(bytes) AS libre_bytes
            FROM dba_free_space
            WHERE tablespace_name = :tablespace
            GROUP BY tablespace_name, file_id
        ),
        uso_temp AS (
            SELECT tablespace_name, file_id, SUM(bytes_used) AS usado_bytes
            FROM v$temp_space_header
            WHERE tablespace_name = :tablespace
            GROUP BY tablespace_name, file_id
        )
        SELECT 'DATAFILE' AS tipo_archivo,
               df.file_id,
               df.file_name,
               ROUND(df.bytes / 1048576, 1) AS tamano_mb,
               ROUND((df.bytes - NVL(ld.libre_bytes, 0)) / 1048576, 1) AS usado_mb,
               ROUND(NVL(ld.libre_bytes, 0) / 1048576, 1) AS libre_mb,
               ROUND((df.bytes - NVL(ld.libre_bytes, 0)) / NULLIF(df.bytes, 0) * 100, 1) AS pct_usado,
               df.autoextensible,
               ROUND(CASE WHEN df.maxbytes > 0 THEN df.maxbytes ELSE df.bytes END / 1048576, 1) AS max_mb,
               NVL(df.online_status, df.status) AS estado
        FROM dba_data_files df
        LEFT JOIN libre_data ld
          ON ld.tablespace_name = df.tablespace_name
         AND ld.file_id = df.file_id
        WHERE df.tablespace_name = :tablespace

        UNION ALL

        SELECT 'TEMPFILE' AS tipo_archivo,
               tf.file_id,
               tf.file_name,
               ROUND(tf.bytes / 1048576, 1) AS tamano_mb,
               ROUND(LEAST(tf.bytes, GREATEST(0, NVL(ut.usado_bytes, 0))) / 1048576, 1) AS usado_mb,
               ROUND((tf.bytes - LEAST(tf.bytes, GREATEST(0, NVL(ut.usado_bytes, 0)))) / 1048576, 1) AS libre_mb,
               ROUND(LEAST(tf.bytes, GREATEST(0, NVL(ut.usado_bytes, 0))) / NULLIF(tf.bytes, 0) * 100, 1) AS pct_usado,
               tf.autoextensible,
               ROUND(CASE WHEN tf.maxbytes > 0 THEN tf.maxbytes ELSE tf.bytes END / 1048576, 1) AS max_mb,
               tf.status AS estado
        FROM dba_temp_files tf
        LEFT JOIN uso_temp ut
          ON ut.tablespace_name = tf.tablespace_name
         AND ut.file_id = tf.file_id
        WHERE tf.tablespace_name = :tablespace
        ORDER BY file_id
        """;
}

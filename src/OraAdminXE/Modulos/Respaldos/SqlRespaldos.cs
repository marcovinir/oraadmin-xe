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

    /// <summary>Marca el respaldo con error y agrega el mensaje de Oracle a la observación (máx. 500).</summary>
    public const string MarcarError = """
        UPDATE admin_bd.respaldo
        SET estado = 'ERROR', fecha_fin = SYSDATE,
            observacion = SUBSTR(NVL2(observacion, observacion || ' | ', '') || :mensaje, 1, 500)
        WHERE id_respaldo = :id
        """;
}

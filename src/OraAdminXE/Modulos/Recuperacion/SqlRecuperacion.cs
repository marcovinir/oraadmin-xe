namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>Consultas del módulo de Recuperación (probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlRecuperacion
{
    /// <summary>Respaldos que se pueden restaurar, con sus objetos en una sola columna.</summary>
    public const string RespaldosCompletados = """
        SELECT v.id_respaldo, v.tipo, v.directorio, v.ruta_so, v.nombre_job, v.archivo_dump, v.archivo_log,
               v.fecha_inicio, v.tamano_mb, v.observacion,
               (SELECT LISTAGG(NVL2(o.tabla, o.esquema || '.' || o.tabla, o.esquema), ', ')
                       WITHIN GROUP (ORDER BY o.esquema, o.tabla)
                FROM admin_bd.respaldo_objeto o WHERE o.id_respaldo = v.id_respaldo) AS objetos
        FROM admin_bd.v_respaldos v
        WHERE v.estado = 'COMPLETADO'
        ORDER BY v.fecha_inicio DESC, v.id_respaldo DESC
        """;

    public const string ObjetosDe = "SELECT esquema, tabla FROM admin_bd.respaldo_objeto WHERE id_respaldo = :id ORDER BY esquema, tabla";

    /// <summary>Esquemas de Oracle (SYS, AUDSYS…): no se ofrecen para restaurar desde un respaldo full.</summary>
    public const string EsquemasDeOracle = "SELECT username FROM dba_users WHERE oracle_maintained = 'Y'";

    public const string TablasDe = "SELECT table_name FROM dba_tables WHERE owner = :e ORDER BY table_name";

    public const string ExisteTabla = "SELECT COUNT(*) FROM dba_tables WHERE owner = :e AND table_name = :t";

    public const string InsertarRestauracion = """
        INSERT INTO admin_bd.restauracion (id_respaldo, nombre_job, esquema_destino, accion_existente, archivo_log, estado, usuario_bd)
        VALUES (:respaldo, :job, :destino, :accion, :log, 'EN_PROCESO', USER)
        RETURNING id_restauracion INTO :id
        """;

    public const string InsertarObjeto = """
        INSERT INTO admin_bd.restauracion_objeto (id_restauracion, esquema, tabla) VALUES (:id, :esquema, :tabla)
        """;

    public const string Terminar = """
        UPDATE admin_bd.restauracion
        SET estado = :estado, fecha_fin = SYSDATE, observacion = :obs
        WHERE id_restauracion = :id
        """;

    public const string MarcarError = """
        UPDATE admin_bd.restauracion
        SET estado = 'ERROR', fecha_fin = SYSDATE, observacion = SUBSTR(:mensaje, 1, 500)
        WHERE id_restauracion = :id
        """;
}

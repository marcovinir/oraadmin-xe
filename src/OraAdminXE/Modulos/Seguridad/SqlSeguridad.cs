namespace OraAdminXE.Modulos.Seguridad;

/// <summary>Consultas del módulo de Seguridad (probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlSeguridad
{
    /// <summary>Usuarios; con :todos = 0 solo los que no son de Oracle.</summary>
    public const string Usuarios = """
        SELECT u.username, u.account_status, u.default_tablespace, u.profile,
               CASE WHEN q.max_bytes = -1 THEN 'ILIMITADA' ELSE TO_CHAR(ROUND(q.max_bytes / 1048576)) END AS cuota_mb,
               u.created, u.lock_date, u.expiry_date, u.oracle_maintained
        FROM dba_users u
        LEFT JOIN dba_ts_quotas q ON q.username = u.username AND q.tablespace_name = u.default_tablespace
        WHERE (:todos = 1 OR u.oracle_maintained = 'N')
        ORDER BY u.oracle_maintained, u.username
        """;

    public const string TablespacesPermanentes = """
        SELECT tablespace_name FROM dba_tablespaces
        WHERE contents = 'PERMANENT' AND status = 'ONLINE' AND tablespace_name NOT IN ('SYSTEM', 'SYSAUX')
        ORDER BY tablespace_name
        """;

    public const string Perfiles = "SELECT DISTINCT profile FROM dba_profiles ORDER BY profile";

    public const string EstadoUsuario = "SELECT account_status FROM dba_users WHERE username = :u";
}

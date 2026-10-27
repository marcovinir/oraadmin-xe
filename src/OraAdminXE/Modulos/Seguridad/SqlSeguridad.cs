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

    /// <summary>Roles con cuántos usuarios o roles lo tienen; los de Oracle al final.</summary>
    public const string Roles = """
        SELECT r.role, r.oracle_maintained,
               (SELECT COUNT(*) FROM dba_role_privs p WHERE p.granted_role = r.role) AS otorgado_a
        FROM dba_roles r
        WHERE (:todos = 1 OR r.oracle_maintained = 'N')
        ORDER BY r.oracle_maintained, r.role
        """;

    /// <summary>A quiénes se les puede otorgar algo: usuarios y roles que no son de Oracle.</summary>
    public const string Beneficiarios = """
        SELECT username AS nombre, 'USUARIO' AS tipo FROM dba_users WHERE oracle_maintained = 'N'
        UNION ALL
        SELECT role, 'ROL' FROM dba_roles WHERE oracle_maintained = 'N'
        ORDER BY 2 DESC, 1
        """;

    /// <summary>Todo lo que tiene un usuario o rol: roles, privilegios de sistema y de objeto.</summary>
    public const string PrivilegiosDe = """
        SELECT 'ROL' AS tipo, granted_role AS privilegio, CAST(NULL AS VARCHAR2(261)) AS objeto, admin_option AS con_opcion
        FROM dba_role_privs WHERE grantee = :g
        UNION ALL
        SELECT 'SISTEMA', privilege, NULL, admin_option FROM dba_sys_privs WHERE grantee = :g
        UNION ALL
        SELECT 'OBJETO', privilege, owner || '.' || table_name, grantable FROM dba_tab_privs WHERE grantee = :g AND type = 'TABLE'
        ORDER BY 1, 2, 3
        """;

    public const string TodosLosRoles = "SELECT role FROM dba_roles ORDER BY oracle_maintained, role";

    public const string PrivilegiosSistema = "SELECT name FROM system_privilege_map ORDER BY name";

    public const string Esquemas = "SELECT username FROM dba_users WHERE oracle_maintained = 'N' ORDER BY username";

    public const string TablasDe = "SELECT table_name FROM dba_tables WHERE owner = :e ORDER BY table_name";
}

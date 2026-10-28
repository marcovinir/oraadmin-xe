namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Consultas de solo lectura para políticas de auditoría unificada en la PDB actual.</summary>
public static class SqlAuditoria
{
    public const string Diagnostico = """
        SELECT SYS_CONTEXT('USERENV', 'CON_NAME') AS contenedor,
               CASE WHEN EXISTS (SELECT 1 FROM session_roles WHERE role = 'AUDIT_ADMIN') THEN 'SI' ELSE 'NO' END AS audit_admin,
               CASE WHEN EXISTS (SELECT 1 FROM session_privs WHERE privilege = 'AUDIT SYSTEM') THEN 'SI' ELSE 'NO' END AS audit_system
        FROM dual
        """;

    public const string Politicas = """
        WITH definiciones AS (
            SELECT policy_name, MAX(oracle_supplied) AS oracle_supplied, MAX(common) AS common,
                   MAX(inherited) AS inherited, COUNT(*) AS cantidad_acciones
            FROM audit_unified_policies
            GROUP BY policy_name
        ), alcances AS (
            SELECT policy_name,
                   LISTAGG(enabled_option || ': ' || entity_name || ' (' || entity_type || ')', ' | ')
                       WITHIN GROUP (ORDER BY enabled_option, entity_name) AS alcance,
                   LISTAGG('Éxito=' || success || ', fallo=' || failure, ' | ')
                       WITHIN GROUP (ORDER BY enabled_option, entity_name) AS resultado,
                   COUNT(*) AS cantidad_alcances
            FROM audit_unified_enabled_policies
            GROUP BY policy_name
        )
        SELECT d.policy_name,
               CASE WHEN a.policy_name IS NULL THEN 'NO' ELSE 'SI' END AS activa,
               NVL(a.alcance, '—') AS alcance,
               NVL(a.resultado, '—') AS configuracion,
               d.cantidad_acciones, d.oracle_supplied, d.common, d.inherited,
               NVL(a.cantidad_alcances, 0) AS cantidad_alcances
        FROM definiciones d
        LEFT JOIN alcances a ON a.policy_name = d.policy_name
        ORDER BY d.oracle_supplied DESC, d.policy_name
        """;

    public const string Detalle = """
        SELECT audit_option, audit_option_type, object_schema, object_name, object_type,
               audit_only_toplevel, audit_condition, condition_eval_opt
        FROM audit_unified_policies
        WHERE policy_name = :politica
        ORDER BY audit_option_type, audit_option, object_schema, object_name
        """;

    public const string Politica = """
        SELECT policy_name, MAX(oracle_supplied) AS oracle_supplied, MAX(common) AS common, MAX(inherited) AS inherited
        FROM audit_unified_policies
        WHERE policy_name = :politica
        GROUP BY policy_name
        """;

    public const string Alcances = """
        SELECT enabled_option, entity_name, entity_type, success, failure
        FROM audit_unified_enabled_policies
        WHERE policy_name = :politica
        ORDER BY enabled_option, entity_name
        """;

    public const string ExisteTabla = "SELECT COUNT(*) FROM dba_tables WHERE owner = :esquema AND table_name = :tabla";
    public const string ExisteUsuario = "SELECT COUNT(*) FROM dba_users WHERE username = :usuario";
    public const string ExistePolitica = "SELECT COUNT(*) FROM audit_unified_policies WHERE policy_name = :politica";

    public const string OraLogonFailures = """
        SELECT p.policy_name, MAX(p.oracle_supplied) AS oracle_supplied,
               MAX(CASE WHEN p.audit_option = 'LOGON' THEN 1 ELSE 0 END) AS incluye_logon,
               COUNT(e.policy_name) AS alcances_habilitados,
               SUM(CASE WHEN e.failure = 'YES' THEN 1 ELSE 0 END) AS alcances_con_fallos
        FROM audit_unified_policies p
        LEFT JOIN audit_unified_enabled_policies e ON e.policy_name = p.policy_name
        WHERE p.policy_name = 'ORA_LOGON_FAILURES'
        GROUP BY p.policy_name
        """;
}

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>Consultas de la pestaña Perfiles (issue #10, probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlPerfiles
{
    /// <summary>Perfiles con cuántos usuarios los usan; DEFAULT primero y los de Oracle al final.</summary>
    public const string Perfiles = """
        SELECT p.profile, (SELECT COUNT(*) FROM dba_users u WHERE u.profile = p.profile) AS usuarios
        FROM (SELECT DISTINCT profile FROM dba_profiles) p
        ORDER BY CASE WHEN p.profile = 'DEFAULT' THEN 0 WHEN p.profile LIKE 'ORA\_%' ESCAPE '\' THEN 2 ELSE 1 END, p.profile
        """;

    /// <summary>Límites de un perfil: primero los de contraseña.</summary>
    public const string LimitesDe = """
        SELECT resource_name, resource_type, limit
        FROM dba_profiles WHERE profile = :p
        ORDER BY resource_type DESC, resource_name
        """;

    /// <summary>Usuarios propios con su perfil actual (a quién se le puede asignar uno).</summary>
    public const string Usuarios = "SELECT username, profile FROM dba_users WHERE oracle_maintained = 'N' ORDER BY username";

    public const string EstadoUsuario = "SELECT account_status FROM dba_users WHERE username = :u";
}

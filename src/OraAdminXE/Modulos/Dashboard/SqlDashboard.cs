namespace OraAdminXE.Modulos.Dashboard;

/// <summary>Consultas de la pantalla de Inicio (probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlDashboard
{
    public const string Instancia = """
        SELECT i.instance_name, i.version_full, i.status, i.startup_time,
               SYSDATE - i.startup_time AS dias_arriba,
               SYS_CONTEXT('USERENV','CON_NAME') AS contenedor
        FROM v$instance i
        """;

    public const string Sesiones = """
        SELECT COUNT(CASE WHEN status = 'ACTIVE' THEN 1 END) AS activas, COUNT(*) AS total
        FROM v$session WHERE type = 'USER'
        """;

    public const string UltimoRespaldo = """
        SELECT tipo, fecha_inicio, estado, tamano_mb
        FROM admin_bd.v_respaldos ORDER BY fecha_inicio DESC FETCH FIRST 1 ROW ONLY
        """;

    /// <summary>Uso de los tablespaces permanentes y de undo, del más lleno al menos lleno.</summary>
    public const string Tablespaces = """
        SELECT t.tablespace_name, t.contents, t.status,
               ROUND(df.total / 1048576) AS total_mb,
               ROUND((df.total - NVL(fs.libre, 0)) / 1048576) AS usado_mb,
               ROUND((df.total - NVL(fs.libre, 0)) / df.total * 100, 1) AS pct_usado,
               df.autoextend
        FROM dba_tablespaces t
        JOIN (SELECT tablespace_name, SUM(bytes) total, MAX(autoextensible) autoextend
              FROM dba_data_files GROUP BY tablespace_name) df ON df.tablespace_name = t.tablespace_name
        LEFT JOIN (SELECT tablespace_name, SUM(bytes) libre
                   FROM dba_free_space GROUP BY tablespace_name) fs ON fs.tablespace_name = t.tablespace_name
        ORDER BY pct_usado DESC
        """;

    /// <summary>Datos de usuario (esquemas que no son de Oracle) frente al límite de 12 GB de XE.</summary>
    public const string DatosUsuario = """
        SELECT NVL(ROUND(SUM(s.bytes) / 1073741824, 2), 0) AS gb
        FROM dba_segments s
        WHERE s.owner IN (SELECT username FROM dba_users WHERE oracle_maintained = 'N')
        """;

    public const double LimiteXeGb = 12;
}

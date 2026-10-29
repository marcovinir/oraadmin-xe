namespace OraAdminXE.Modulos.Directorios;

/// <summary>Consultas del módulo Directorios. Las rutas se leen del diccionario de la PDB activa.</summary>
public static class SqlDirectorios
{
    /// <summary>Objetos DIRECTORY visibles para la PDB y su posible registro de ADMIN_BD.</summary>
    public const string Directorios = """
        SELECT d.directory_name, d.directory_path, d.origin_con_id,
               c.id_directorio, c.ruta_so AS ruta_catalogo, c.fecha_registro,
               CASE WHEN c.id_directorio IS NULL THEN 'NO' ELSE 'SI' END AS registrado,
               CASE WHEN c.id_directorio IS NULL THEN 'NO' WHEN c.activo = 'S' THEN 'SI' ELSE 'NO' END AS activo
        FROM dba_directories d
        LEFT JOIN admin_bd.directorio c ON c.nombre_oracle = d.directory_name
        ORDER BY d.directory_name
        """;

    public const string DirectorioPorNombre = """
        SELECT d.directory_name, d.directory_path, d.origin_con_id,
               c.id_directorio, c.ruta_so AS ruta_catalogo, c.fecha_registro,
               CASE WHEN c.id_directorio IS NULL THEN 'NO' ELSE 'SI' END AS registrado,
               CASE WHEN c.id_directorio IS NULL THEN 'NO' WHEN c.activo = 'S' THEN 'SI' ELSE 'NO' END AS activo
        FROM dba_directories d
        LEFT JOIN admin_bd.directorio c ON c.nombre_oracle = d.directory_name
        WHERE d.directory_name = :nombre
        """;

    public const string ExisteDirectorio = "SELECT COUNT(*) FROM dba_directories WHERE directory_name = :nombre";

    /// <summary>Privilegios de objeto, incluidos los otorgados a PUBLIC.</summary>
    public const string PrivilegiosDe = """
        SELECT grantee, privilege, grantable
        FROM dba_tab_privs
        WHERE table_name = :directorio
          AND type = 'DIRECTORY'
        ORDER BY grantee, privilege
        """;

    /// <summary>Comprueba solo grants directos o públicos de READ/WRITE para ADMIN_BD; no expande roles.</summary>
    public const string PrivilegiosDirectosOPublicosAdminBd = """
        SELECT DISTINCT privilege
        FROM dba_tab_privs
        WHERE table_name = :directorio
          AND type = 'DIRECTORY'
          AND grantee IN ('ADMIN_BD', 'PUBLIC')
          AND privilege IN ('READ', 'WRITE')
        """;

    /// <summary>Usuario existente y si Oracle lo mantiene internamente.</summary>
    public const string Usuario = "SELECT username, account_status, oracle_maintained FROM dba_users WHERE username = :usuario";

    /// <summary>
    /// Estado que se vuelve a consultar justo antes de eliminar. Los jobs se pueden asociar de forma
    /// fiable cuando su nombre y dueño coinciden con una fila del catálogo de respaldos.
    /// </summary>
    public const string EstadoEliminar = """
        SELECT d.directory_name, d.directory_path, c.id_directorio, c.ruta_so AS ruta_catalogo, c.activo,
               (SELECT COUNT(*) FROM admin_bd.respaldo r
                WHERE r.id_directorio = c.id_directorio
                  AND NVL(r.estado, '?') <> 'ELIMINADO') AS respaldos_restaurables,
               (SELECT COUNT(*)
                FROM admin_bd.restauracion x
                JOIN admin_bd.respaldo r ON r.id_respaldo = x.id_respaldo
                WHERE r.id_directorio = c.id_directorio
                  AND NVL(x.estado, '?') = 'EN_PROCESO') AS restauraciones_activas,
               (SELECT COUNT(*) FROM admin_bd.programacion p
                WHERE p.id_directorio = c.id_directorio AND p.activo = 'S') AS programaciones_activas,
               (SELECT COUNT(*)
                FROM admin_bd.respaldo r
                JOIN dba_datapump_jobs j ON j.owner_name = r.usuario_bd AND j.job_name = r.nombre_job
                WHERE r.id_directorio = c.id_directorio AND j.state = 'EXECUTING') AS jobs_ejecutando
        FROM dba_directories d
        LEFT JOIN admin_bd.directorio c ON c.nombre_oracle = d.directory_name
        WHERE d.directory_name = :nombre
        """;

    /// <summary>Inserta un catálogo nuevo o reactiva el histórico sin cambiar su identidad.</summary>
    public const string Registrar = """
        MERGE INTO admin_bd.directorio d
        USING (SELECT :nombre AS nombre_oracle, :ruta AS ruta_so FROM dual) s
        ON (d.nombre_oracle = s.nombre_oracle)
        WHEN MATCHED THEN UPDATE SET d.ruta_so = s.ruta_so, d.activo = 'S'
        WHEN NOT MATCHED THEN INSERT (nombre_oracle, ruta_so, activo)
          VALUES (s.nombre_oracle, s.ruta_so, 'S')
        """;

    public const string Desactivar = "UPDATE admin_bd.directorio SET activo = 'N' WHERE id_directorio = :id AND activo = 'S'";
}

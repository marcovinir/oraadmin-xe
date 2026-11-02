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

    /// <summary>DIRECTORY activos que aún existen en Oracle. La igualdad de rutas se verifica en C# tras normalizarlas.</summary>
    public const string DirectoriosActivosArchivos = """
        SELECT c.id_directorio, c.nombre_oracle, c.ruta_so AS ruta_catalogo, d.directory_path, d.origin_con_id
        FROM admin_bd.directorio c
        LEFT JOIN dba_directories d ON d.directory_name = c.nombre_oracle
        WHERE c.activo = 'S'
        ORDER BY c.nombre_oracle
        """;

    /// <summary>Archivos conocidos; la asociación siempre conserva el id del DIRECTORY.</summary>
    public const string CatalogoArchivosDeDirectorio = """
        SELECT r.id_respaldo, r.id_directorio, r.nombre_job, r.estado, r.archivo_dump AS archivo, 'DMP' AS tipo,
               0 AS restauracion_activa
        FROM admin_bd.respaldo r
        WHERE r.id_directorio = :id_directorio
        UNION ALL
        SELECT r.id_respaldo, r.id_directorio, r.nombre_job, r.estado, r.archivo_log AS archivo, 'LOG_RESPALDO' AS tipo,
               0 AS restauracion_activa
        FROM admin_bd.respaldo r
        WHERE r.id_directorio = :id_directorio
        UNION ALL
        SELECT r.id_respaldo, r.id_directorio, x.nombre_job, x.estado, x.archivo_log AS archivo, 'LOG_RESTAURACION' AS tipo,
               CASE WHEN x.estado = 'EN_PROCESO' THEN 1 ELSE 0 END AS restauracion_activa
        FROM admin_bd.restauracion x
        JOIN admin_bd.respaldo r ON r.id_respaldo = x.id_respaldo
        WHERE r.id_directorio = :id_directorio
        """;

    /// <summary>
    /// Relee la asociación y todos los bloqueos antes de mutar. Un scheduler ejecutándose en el
    /// mismo DIRECTORY bloquea de forma deliberadamente conservadora la operación.
    /// </summary>
    public const string RespaldoParaOperacion = """
        SELECT r.id_respaldo, r.id_directorio, r.nombre_job, r.archivo_dump, r.estado,
               c.nombre_oracle, c.ruta_so AS ruta_catalogo, d.directory_path,
               CASE WHEN EXISTS (
                    SELECT 1 FROM dba_datapump_jobs j
                    WHERE j.owner_name = r.usuario_bd AND j.job_name = r.nombre_job AND j.state = 'EXECUTING'
               ) THEN 1 ELSE 0 END AS job_datapump_activo,
               CASE WHEN EXISTS (
                    SELECT 1
                    FROM admin_bd.programacion p
                    JOIN dba_scheduler_running_jobs s ON s.owner = USER AND s.job_name = p.nombre_job
                    WHERE p.id_directorio = r.id_directorio AND p.activo = 'S'
               ) THEN 1 ELSE 0 END AS job_scheduler_activo,
               CASE WHEN EXISTS (
                    SELECT 1 FROM admin_bd.restauracion x
                    WHERE x.id_respaldo = r.id_respaldo AND x.estado = 'EN_PROCESO'
               ) THEN 1 ELSE 0 END AS restauracion_activa
        FROM admin_bd.respaldo r
        JOIN admin_bd.directorio c ON c.id_directorio = r.id_directorio AND c.activo = 'S'
        JOIN dba_directories d ON d.directory_name = c.nombre_oracle
        WHERE r.id_respaldo = :id_respaldo AND r.id_directorio = :id_directorio
        """;

    public const string RenombrarDumpCatalogo = """
        UPDATE admin_bd.respaldo
        SET archivo_dump = :archivo_destino
        WHERE id_respaldo = :id_respaldo
          AND id_directorio = :id_directorio
          AND UPPER(archivo_dump) = UPPER(:archivo_origen)
          AND estado = 'COMPLETADO'
        """;

    public const string MarcarRespaldoEliminado = """
        UPDATE admin_bd.respaldo
        SET estado = 'ELIMINADO',
            observacion = SUBSTR(NVL2(observacion, observacion || ' | ', '') || :observacion, 1, 500)
        WHERE id_respaldo = :id_respaldo
          AND id_directorio = :id_directorio
          AND UPPER(archivo_dump) = UPPER(:archivo_dump)
          AND estado = 'COMPLETADO'
        """;

    public const string RenombrarArchivoFisico = """
        BEGIN
          UTL_FILE.FRENAME(:dir_origen, :archivo_origen, :dir_destino, :archivo_destino, FALSE);
        END;
        """;

    public const string EliminarArchivoFisico = """
        BEGIN
          UTL_FILE.FREMOVE(:directorio, :archivo);
        END;
        """;

    /// <summary>Se exige READ/WRITE del DIRECTORY y EXECUTE directo o PUBLIC sobre UTL_FILE.</summary>
    public const string PrivilegiosArchivosSesion = """
        SELECT 'DIRECTORY_' || privilege AS privilegio
        FROM dba_tab_privs
        WHERE table_name = :directorio AND type = 'DIRECTORY'
          AND grantee IN (USER, 'PUBLIC') AND privilege IN ('READ', 'WRITE')
        UNION ALL
        SELECT 'UTL_FILE' AS privilegio
        FROM dba_tab_privs
        WHERE owner = 'SYS' AND table_name = 'UTL_FILE' AND type = 'PACKAGE'
          AND grantee IN (USER, 'PUBLIC') AND privilege = 'EXECUTE'
        """;
}

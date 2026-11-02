namespace OraAdminXE.Modulos.Respaldos;

/// <summary>Consultas de los respaldos programados (issue #30, probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlProgramaciones
{
    /// <summary>
    /// Programaciones cruzadas con el scheduler: estado, próxima y última ejecución, veces que corrió y que falló.
    /// Las fechas del scheduler son TIMESTAMP WITH TIME ZONE: se pasan a DATE para la grilla.
    /// </summary>
    public const string Lista = """
        SELECT p.id_programacion, p.nombre_job, t.codigo AS tipo, d.nombre_oracle AS directorio, p.frecuencia, p.activo,
               (SELECT LISTAGG(NVL2(o.tabla, o.esquema || '.' || o.tabla, o.esquema), ', ')
                       WITHIN GROUP (ORDER BY o.esquema, o.tabla)
                FROM admin_bd.programacion_objeto o WHERE o.id_programacion = p.id_programacion) AS objetos,
               j.state, CAST(j.next_run_date AS DATE) AS proxima, CAST(j.last_start_date AS DATE) AS ultima,
               j.run_count, j.failure_count
        FROM admin_bd.programacion p
        JOIN admin_bd.tipo_respaldo t ON t.id_tipo = p.id_tipo
        JOIN admin_bd.directorio d ON d.id_directorio = p.id_directorio
        LEFT JOIN dba_scheduler_jobs j ON j.owner = USER AND j.job_name = p.nombre_job
        ORDER BY p.nombre_job
        """;

    /// <summary>¿El nombre ya lo usa otra programación o un job del scheduler?</summary>
    public const string NombreOcupado = """
        SELECT (SELECT COUNT(*) FROM admin_bd.programacion WHERE nombre_job = :n)
             + (SELECT COUNT(*) FROM dba_scheduler_jobs WHERE owner = USER AND job_name = :n)
        FROM dual
        """;
}

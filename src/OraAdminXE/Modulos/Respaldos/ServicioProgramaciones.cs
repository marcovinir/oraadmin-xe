using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>Cada cuánto corre un respaldo programado.</summary>
public enum TipoFrecuencia { CadaHoras, Diaria, Semanal }

/// <summary>Frecuencia elegida en el diálogo; <see cref="RepeatInterval"/> la traduce al calendario de DBMS_SCHEDULER.</summary>
public sealed record Frecuencia(TipoFrecuencia Tipo, int Horas = 1, int Hora = 22, int Minuto = 0, DayOfWeek Dia = DayOfWeek.Sunday)
{
    private static readonly string[] Dias = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    /// <summary>
    /// repeat_interval. Siempre lleva BYMINUTE y BYSECOND para que el primer respaldo no corra en el momento de crearlo
    /// (el scheduler toma la primera coincidencia del calendario a partir de ahora).
    /// </summary>
    public string RepeatInterval => Tipo switch
    {
        TipoFrecuencia.CadaHoras => $"FREQ=HOURLY;INTERVAL={Math.Clamp(Horas, 1, 23)};BYMINUTE=0;BYSECOND=0",
        TipoFrecuencia.Diaria => $"FREQ=DAILY;BYHOUR={Hora};BYMINUTE={Minuto};BYSECOND=0",
        _ => $"FREQ=WEEKLY;BYDAY={Dias[(int)Dia]};BYHOUR={Hora};BYMINUTE={Minuto};BYSECOND=0"
    };
}

/// <summary>
/// Respaldos programados con DBMS_SCHEDULER (issue #30). Arma el bloque que corre el job (catálogo + Data Pump + bitácora)
/// y los bloques para crear, activar, desactivar, ejecutar ya y eliminar la programación.
/// El job corre en el servidor aunque la app esté cerrada; sus respaldos aparecen en el catálogo como cualquier otro.
/// </summary>
public static class ServicioProgramaciones
{
    /// <summary>job_action de DBMS_SCHEDULER.CREATE_JOB admite como máximo 4000 caracteres.</summary>
    public const int MaxAccion = 4000;

    /// <summary>
    /// Bloque que ejecuta el job en cada corrida. Los archivos llevan la fecha (PRG_X_yyyyMMdd_HH24MISS) para no repetirse.
    /// Si Data Pump falla, saca el detalle con GET_STATUS, deja el respaldo en ERROR, lo anota en la bitácora y relanza
    /// el error para que el scheduler cuente la falla (FAILURE_COUNT).
    /// </summary>
    public static string BloqueJob(SolicitudRespaldo s)
    {
        ServicioRespaldos.Validar(s);
        var dir = Identificador.Validar(s.Directorio.NombreOracle, "directorio");
        var job = s.Job;
        if (job.Length > 100)
            throw new ArgumentException("El nombre de la programación admite como máximo 100 caracteres (cada respaldo le suma la fecha).");
        var objetos = ServicioRespaldos.Objetos(s).Select(o =>
            $"  INSERT INTO admin_bd.respaldo_objeto (id_respaldo, esquema, tabla) VALUES (id, '{o.Esquema}', {(o.Tabla is null ? "NULL" : $"'{o.Tabla}'")});");
        var filtros = new List<string>();
        if (s.Tipo != TipoRespaldo.Full) filtros.Add(BloqueDataPump.FiltroEsquemas(s.Esquemas));
        if (s.Tipo == TipoRespaldo.Table) filtros.Add(BloqueDataPump.FiltroTablas(s.Tablas));
        const string bitacora = "INSERT INTO admin_bd.bitacora (id_modulo, usuario_bd, accion, resultado, mensaje) " +
                                "VALUES ((SELECT id_modulo FROM admin_bd.modulo WHERE nombre = 'RESPALDOS'), USER, ";

        var lineas = new List<string>
        {
            "DECLARE",
            "  h NUMBER; e VARCHAR2(30); x BOOLEAN; t NUMBER; b NUMBER; id NUMBER; m VARCHAR2(4000); st ku$_Status; i NUMBER;",
            $"  n VARCHAR2(128) := '{job}_' || TO_CHAR(SYSDATE, 'YYYYMMDD_HH24MISS');",
            "BEGIN",
            "  INSERT INTO admin_bd.respaldo (id_tipo, id_directorio, nombre_job, archivo_dump, archivo_log, estado, usuario_bd, observacion)",
            $"  VALUES ((SELECT id_tipo FROM admin_bd.tipo_respaldo WHERE codigo = '{s.Codigo}'), {s.Directorio.Id}, n, n || '.DMP', n || '.LOG', 'EN_PROCESO', USER, 'Programado: {job}')",
            "  RETURNING id_respaldo INTO id;"
        };
        lineas.AddRange(objetos);
        lineas.AddRange(
        [
            "  COMMIT;",
            $"  h := DBMS_DATAPUMP.OPEN('EXPORT', '{s.Codigo}', NULL, n);",
            $"  DBMS_DATAPUMP.ADD_FILE(h, n || '.LOG', '{dir}', filetype => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);",
            $"  DBMS_DATAPUMP.ADD_FILE(h, n || '.DMP', '{dir}');"
        ]);
        lineas.AddRange(filtros.Select(f => "  " + f));
        lineas.AddRange(
        [
            "  DBMS_DATAPUMP.START_JOB(h);",
            "  DBMS_DATAPUMP.WAIT_FOR_JOB(h, e);",
            $"  UTL_FILE.FGETATTR('{dir}', n || '.DMP', x, t, b);",
            "  UPDATE admin_bd.respaldo SET estado = CASE e WHEN 'COMPLETED' THEN 'COMPLETADO' ELSE 'ERROR' END, fecha_fin = SYSDATE, tamano_bytes = t WHERE id_respaldo = id;",
            $"  {bitacora}'Respaldo programado {job}', CASE e WHEN 'COMPLETED' THEN 'OK' ELSE 'ERROR' END, n || '.DMP: ' || e);",
            "  COMMIT;",
            "EXCEPTION WHEN OTHERS THEN",
            "  m := SQLERRM;",
            "  IF h IS NOT NULL THEN",
            "    BEGIN DBMS_DATAPUMP.GET_STATUS(h, DBMS_DATAPUMP.KU$_STATUS_JOB_ERROR, 0, e, st); i := st.error.FIRST;",
            "      WHILE i IS NOT NULL LOOP m := SUBSTR(m || CHR(10) || st.error(i).LogText, 1, 4000); i := st.error.NEXT(i); END LOOP;",
            "    EXCEPTION WHEN OTHERS THEN NULL; END;",
            "    BEGIN DBMS_DATAPUMP.STOP_JOB(h, 1, 0); EXCEPTION WHEN OTHERS THEN NULL; END;",
            "  END IF;",
            "  UPDATE admin_bd.respaldo SET estado = 'ERROR', fecha_fin = SYSDATE, observacion = SUBSTR(m, 1, 500) WHERE id_respaldo = id;",
            $"  {bitacora}'Respaldo programado {job}', 'ERROR', m);",
            "  COMMIT;",
            "  RAISE;",
            "END;"
        ]);
        var bloque = string.Join("\n", lineas);
        if (bloque.Length > MaxAccion)
            throw new ArgumentException($"El job quedaría con {bloque.Length} caracteres y DBMS_SCHEDULER admite {MaxAccion}. Elija menos tablas o esquemas.");
        return bloque;
    }

    /// <summary>
    /// Crea la programación: filas en PROGRAMACION / PROGRAMACION_OBJETO y el job habilitado.
    /// El job_action va con comillas alternativas q'[ … ]' para que se lea igual que el bloque del job.
    /// </summary>
    public static string BloqueCrear(SolicitudRespaldo s, Frecuencia f)
    {
        var accion = BloqueJob(s);
        if (accion.Contains("]'")) throw new InvalidOperationException("El bloque del job no puede contener «]'» (cerraría las comillas q'[ ]').");
        var job = s.Job;
        var lineas = new List<string>
        {
            "DECLARE",
            "  id NUMBER;",
            "BEGIN",
            "  INSERT INTO admin_bd.programacion (id_tipo, id_directorio, nombre_job, frecuencia, activo)",
            $"  VALUES ((SELECT id_tipo FROM admin_bd.tipo_respaldo WHERE codigo = '{s.Codigo}'), {s.Directorio.Id}, '{job}', '{f.RepeatInterval}', 'S')",
            "  RETURNING id_programacion INTO id;"
        };
        lineas.AddRange(ServicioRespaldos.Objetos(s).Select(o =>
            $"  INSERT INTO admin_bd.programacion_objeto (id_programacion, esquema, tabla) VALUES (id, '{o.Esquema}', {(o.Tabla is null ? "NULL" : $"'{o.Tabla}'")});"));
        lineas.AddRange(
        [
            "  DBMS_SCHEDULER.CREATE_JOB(",
            $"    job_name        => '{job}',",
            "    job_type        => 'PLSQL_BLOCK',",
            "    job_action      => q'[" + accion + "]',",
            $"    repeat_interval => '{f.RepeatInterval}',",
            "    enabled         => TRUE,",
            $"    comments        => 'OraAdmin XE: respaldo {s.Codigo} programado');",
            "END;"
        ]);
        return string.Join("\n", lineas);
    }

    public static string BloqueActivar(string job, bool activar)
    {
        var j = Identificador.Validar(job, "job");
        return activar
            ? $"BEGIN\n  DBMS_SCHEDULER.ENABLE('{j}');\n  UPDATE admin_bd.programacion SET activo = 'S' WHERE nombre_job = '{j}';\nEND;"
            : $"BEGIN\n  DBMS_SCHEDULER.DISABLE('{j}');\n  UPDATE admin_bd.programacion SET activo = 'N' WHERE nombre_job = '{j}';\nEND;";
    }

    /// <summary>Corre el job ya, en segundo plano (no espera a que termine).</summary>
    public static string BloqueEjecutarAhora(string job) =>
        $"BEGIN\n  DBMS_SCHEDULER.RUN_JOB('{Identificador.Validar(job, "job")}', use_current_session => FALSE);\nEND;";

    /// <summary>Borra el job (force: aunque esté corriendo) y la programación; los respaldos ya hechos quedan en el catálogo.</summary>
    public static string BloqueEliminar(string job, bool existeJob)
    {
        var j = Identificador.Validar(job, "job");
        var drop = existeJob ? $"  DBMS_SCHEDULER.DROP_JOB('{j}', force => TRUE);\n" : "";
        return $"BEGIN\n{drop}  DELETE FROM admin_bd.programacion WHERE nombre_job = '{j}';\nEND;";
    }
}

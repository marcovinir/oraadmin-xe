using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Arma los bloques PL/SQL de <c>DBMS_DATAPUMP</c> que usan Respaldos (export) y Recuperación (import).
/// El bloque devuelve el estado final del job en el bind de salida <c>:estado</c> ('COMPLETED' si salió bien).
/// Los nombres de objetos llegan ya validados con <see cref="Identificador"/>, así que no pueden traer comillas.
/// </summary>
public static class BloqueDataPump
{
    /// <summary>
    /// Data Pump solo lanza un ORA-39001 genérico: el detalle (ORA-31641, ORA-39070…) se pide con GET_STATUS.
    /// Además el job a medias se detiene, para que no quede colgado en DBA_DATAPUMP_JOBS.
    /// </summary>
    private static readonly string[] ManejoDeErrores =
    [
        "EXCEPTION",
        "  WHEN OTHERS THEN",
        "    DECLARE",
        "      mensaje VARCHAR2(4000) := SQLERRM;",
        "      st      ku$_Status;",
        "      i       NUMBER;",
        "    BEGIN",
        "      IF h IS NOT NULL THEN",
        "        BEGIN",
        "          DBMS_DATAPUMP.GET_STATUS(h, DBMS_DATAPUMP.KU$_STATUS_JOB_ERROR, 0, estado, st);",
        "          i := st.error.FIRST;",
        "          WHILE i IS NOT NULL LOOP",
        "            mensaje := SUBSTR(mensaje || CHR(10) || st.error(i).LogText, 1, 4000);",
        "            i := st.error.NEXT(i);",
        "          END LOOP;",
        "        EXCEPTION WHEN OTHERS THEN NULL;",
        "        END;",
        "        BEGIN DBMS_DATAPUMP.STOP_JOB(h, 1, 0); EXCEPTION WHEN OTHERS THEN NULL; END;",
        "      END IF;",
        "      RAISE_APPLICATION_ERROR(-20001, mensaje);",
        "    END;",
        "END;"
    ];

    /// <summary>
    /// Bloque completo: OPEN, archivos (primero el log, para que un directorio malo dé ORA-39070),
    /// <paramref name="configuracion"/> (filtros y parámetros), START_JOB, WAIT_FOR_JOB y <paramref name="alTerminar"/>.
    /// Las líneas se unen con \n (como SQL*Plus): así el bloque es igual en Windows y en el «Ver SQL».
    /// </summary>
    public static string Armar(string operacion, string modo, string job, string directorio, string archivoDump, string archivoLog,
                               IEnumerable<string> configuracion, IEnumerable<string>? declaraciones = null, IEnumerable<string>? alTerminar = null)
    {
        var lineas = new List<string> { "DECLARE", "  h      NUMBER;", "  estado VARCHAR2(30);" };
        lineas.AddRange((declaraciones ?? []).Select(d => "  " + d));
        lineas.Add("BEGIN");
        lineas.Add($"  h := DBMS_DATAPUMP.OPEN('{operacion}', '{modo}', NULL, {Identificador.Literal(job)});");
        lineas.Add($"  DBMS_DATAPUMP.ADD_FILE(h, {Identificador.Literal(archivoLog)}, {Identificador.Literal(directorio)}, filetype => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);");
        lineas.Add($"  DBMS_DATAPUMP.ADD_FILE(h, {Identificador.Literal(archivoDump)}, {Identificador.Literal(directorio)});");
        lineas.AddRange(configuracion.Select(c => "  " + c));
        lineas.Add("  DBMS_DATAPUMP.START_JOB(h);");
        lineas.Add("  DBMS_DATAPUMP.WAIT_FOR_JOB(h, estado);");
        lineas.Add("  :estado := estado;");
        lineas.AddRange((alTerminar ?? []).Select(a => "  " + a));
        lineas.AddRange(ManejoDeErrores);
        return string.Join("\n", lineas);
    }

    /// <summary><c>DBMS_DATAPUMP.METADATA_FILTER(h, 'SCHEMA_EXPR', 'IN (''A'',''B'')');</c></summary>
    public static string FiltroEsquemas(IEnumerable<string> esquemas) => Filtro("SCHEMA_EXPR", esquemas);

    /// <summary><c>DBMS_DATAPUMP.METADATA_FILTER(h, 'NAME_EXPR', 'IN (''T1'',''T2'')');</c></summary>
    public static string FiltroTablas(IEnumerable<string> tablas) => Filtro("NAME_EXPR", tablas);

    private static string Filtro(string tipo, IEnumerable<string> nombres)
    {
        var lista = string.Join(",", nombres.Select(n => $"''{Identificador.Validar(n)}''"));
        return $"DBMS_DATAPUMP.METADATA_FILTER(h, '{tipo}', 'IN ({lista})');";
    }
}

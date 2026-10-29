using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Consultas exclusivamente de lectura para la pista de auditoría unificada.</summary>
public static class SqlPistaAuditoria
{
    public const int MaximoFilas = 500;
    public const int MaximoUsuarios = 100;
    public const int MaximoPoliticas = 500;
    public const int MaximoVistaPreviaSql = 1_000;
    public const int MaximoDetalleSql = 4_000;

    public static readonly IReadOnlyList<string> AccionesPermitidas =
    ["LOGON", "INSERT", "UPDATE", "DELETE", "CREATE TABLE", "DROP TABLE", "ALTER TABLE", "CREATE USER", "DROP USER", "GRANT",
     "AUDIT", "NOAUDIT", "CREATE AUDIT POLICY", "DROP AUDIT POLICY"];

    public const string HoraBaseDatos = "SELECT LOCALTIMESTAMP FROM dual";
    public const string ContextoPdb = "SELECT SYS_CONTEXT('USERENV', 'CON_NAME') AS contenedor FROM dual";

    public const string UsuariosRecientes = """
        SELECT dbusername
        FROM (
            SELECT dbusername, MAX(event_timestamp) AS ultimo
            FROM unified_audit_trail
            WHERE event_timestamp >= :desde
              AND event_timestamp <= :hasta
              AND dbusername IS NOT NULL
            GROUP BY dbusername
            ORDER BY MAX(event_timestamp) DESC, dbusername
        )
        WHERE ROWNUM <= 100
        """;

    public const string PoliticasDisponibles = """
        SELECT policy_name
        FROM audit_unified_policies
        GROUP BY policy_name
        ORDER BY policy_name
        FETCH FIRST 500 ROWS ONLY
        """;

    /// <summary>
    /// No existe acción de limpieza en esta pantalla: CLEAN_AUDIT_TRAIL se difiere hasta tener retención y autorización explícitas.
    /// </summary>
    public static ConsultaPistaAuditoria CrearConsulta(FiltroPistaAuditoria filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ValidarFiltro(filtro);

        var condiciones = new List<string>
        {
            "event_timestamp >= :desde",
            "event_timestamp <= :hasta"
        };
        var parametros = new List<OracleParameter>
        {
            Parametro.De("desde", filtro.Desde),
            Parametro.De("hasta", filtro.Hasta)
        };

        if (filtro.Usuario is { Length: > 0 })
        {
            condiciones.Add("dbusername = :usuario");
            parametros.Add(Parametro.De("usuario", filtro.Usuario));
        }
        if (filtro.Accion is { Length: > 0 })
        {
            condiciones.Add("action_name = :accion");
            parametros.Add(Parametro.De("accion", filtro.Accion));
        }
        if (filtro.Politica is { Length: > 0 })
        {
            // UNIFIED_AUDIT_POLICIES es una lista CSV de nombres: los delimitadores evitan POL_X dentro de POL_XYZ.
            condiciones.Add("INSTR(',' || REPLACE(unified_audit_policies, ' ', '') || ',', ',' || :politica || ',') > 0");
            parametros.Add(Parametro.De("politica", filtro.Politica));
        }
        if (filtro.SoloFallidos)
            condiciones.Add("return_code <> 0");

        var sql = $"""
            SELECT event_timestamp, event_timestamp_utc, dbusername, os_username, userhost,
                   action_name, object_schema, object_name, return_code, unified_audit_policies,
                   DBMS_LOB.SUBSTR(sql_text, {MaximoVistaPreviaSql}, 1) AS sql_text_preview,
                   dbid, instance_id, sessionid, entry_id, statement_id
            FROM unified_audit_trail
            WHERE {string.Join("\n  AND ", condiciones)}
            ORDER BY event_timestamp DESC, event_timestamp_utc DESC, instance_id DESC, sessionid DESC, entry_id DESC, statement_id DESC
            FETCH FIRST {MaximoFilas} ROWS ONLY
            """;
        return new ConsultaPistaAuditoria(sql, parametros);
    }

    public static ConsultaPistaAuditoria CrearConsultaDetalle(IdentidadEventoAuditoria identidad)
    {
        if (!identidad.EsCompleta)
            throw new InvalidOperationException("El evento no contiene una identidad completa; por seguridad no se consultará otro SQL_TEXT.");

        var sql = $"""
            SELECT DBMS_LOB.SUBSTR(sql_text, {MaximoDetalleSql}, 1) AS sql_text,
                   CASE WHEN sql_text IS NULL THEN 'NO'
                        WHEN DBMS_LOB.GETLENGTH(sql_text) > {MaximoDetalleSql} THEN 'SI'
                        ELSE 'NO' END AS truncado
            FROM unified_audit_trail
            WHERE event_timestamp_utc = :event_timestamp_utc
              AND dbid = :dbid
              AND instance_id = :instance_id
              AND sessionid = :sessionid
              AND entry_id = :entry_id
              AND statement_id = :statement_id
            """;
        return new ConsultaPistaAuditoria(sql,
        [
            Parametro.De("event_timestamp_utc", identidad.EventTimestampUtc),
            Parametro.De("dbid", identidad.Dbid),
            Parametro.De("instance_id", identidad.InstanceId),
            Parametro.De("sessionid", identidad.SessionId),
            Parametro.De("entry_id", identidad.EntryId),
            Parametro.De("statement_id", identidad.StatementId)
        ]);
    }

    public static FiltroPistaAuditoria NormalizarFiltro(DateTime desde, DateTime hasta, string? usuario, string? accion, string? politica, bool soloFallidos) =>
        new(desde, hasta, NormalizarIdentificadorOpcional(usuario, "usuario"), NormalizarAccion(accion),
            NormalizarIdentificadorOpcional(politica, "política"), soloFallidos);

    public static string InterpretarReturnCode(object? valor)
    {
        int? codigo = valor is null or DBNull ? null : Convert.ToInt32(valor);
        return codigo switch
        {
            0 => "Correcto (RETURN_CODE = 0).",
            1017 => "ORA-01017: credenciales no válidas.",
            28000 => "ORA-28000: cuenta bloqueada.",
            null => "RETURN_CODE no disponible.",
            _ => $"Fallo de Oracle (RETURN_CODE = {codigo}); revise el detalle de Oracle."
        };
    }

    public static bool EsFallido(object? valor) => valor is not null and not DBNull && Convert.ToInt32(valor) != 0;

    public static bool TryCrearIdentidad(DataRow? fila, out IdentidadEventoAuditoria identidad)
    {
        identidad = default;
        if (fila is null || fila.IsNull("EVENT_TIMESTAMP_UTC") || fila.IsNull("DBID") || fila.IsNull("INSTANCE_ID") ||
            fila.IsNull("SESSIONID") || fila.IsNull("ENTRY_ID") || fila.IsNull("STATEMENT_ID"))
            return false;
        if (fila["EVENT_TIMESTAMP_UTC"] is not DateTime fecha) return false;
        try
        {
            identidad = new IdentidadEventoAuditoria(fecha, Convert.ToInt64(fila["DBID"]), Convert.ToInt64(fila["INSTANCE_ID"]),
                Convert.ToInt64(fila["SESSIONID"]), Convert.ToInt64(fila["ENTRY_ID"]), Convert.ToInt64(fila["STATEMENT_ID"]));
            return identidad.EsCompleta;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static void ValidarFiltro(FiltroPistaAuditoria filtro)
    {
        if (filtro.Hasta < filtro.Desde)
            throw new ArgumentException("La fecha y hora «Hasta» no puede ser anterior a «Desde».");
        if (filtro.Accion is { Length: > 0 } && !AccionesPermitidas.Contains(filtro.Accion, StringComparer.Ordinal))
            throw new ArgumentException("La acción seleccionada no está permitida.");
    }

    private static string? NormalizarIdentificadorOpcional(string? texto, string queEs) =>
        string.IsNullOrWhiteSpace(texto) || string.Equals(texto, "Todos", StringComparison.OrdinalIgnoreCase)
            ? null
            : Identificador.Validar(texto, queEs);

    private static string? NormalizarAccion(string? accion)
    {
        if (string.IsNullOrWhiteSpace(accion) || string.Equals(accion, "Todas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(accion, "Todos", StringComparison.OrdinalIgnoreCase)) return null;
        var normalizada = accion.Trim().ToUpperInvariant();
        if (!AccionesPermitidas.Contains(normalizada, StringComparer.Ordinal))
            throw new ArgumentException("La acción seleccionada no está permitida.");
        return normalizada;
    }
}

public sealed record FiltroPistaAuditoria(DateTime Desde, DateTime Hasta, string? Usuario, string? Accion, string? Politica, bool SoloFallidos);
public sealed record ConsultaPistaAuditoria(string Sql, IReadOnlyList<OracleParameter> Parametros);

/// <summary>Clave conservadora: si falta cualquier componente no se carga SQL_TEXT de detalle.</summary>
public readonly record struct IdentidadEventoAuditoria(DateTime EventTimestampUtc, long Dbid, long InstanceId, long SessionId, long EntryId, long StatementId)
{
    public bool EsCompleta => EventTimestampUtc != default && Dbid >= 0 && InstanceId >= 0 && SessionId >= 0 && EntryId >= 0 && StatementId >= 0;
}

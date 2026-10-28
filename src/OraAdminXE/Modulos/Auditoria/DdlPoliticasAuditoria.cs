using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Construye exclusivamente las sentencias soportadas de auditoría unificada.</summary>
public static class DdlPoliticasAuditoria
{
    public static readonly IReadOnlyList<string> AccionesObjeto = ["SELECT", "INSERT", "UPDATE", "DELETE"];
    public static readonly IReadOnlyList<string> AccionesSistema =
        ["CREATE TABLE", "DROP TABLE", "ALTER TABLE", "CREATE USER", "DROP USER", "GRANT", "LOGON"];

    public static string CrearObjeto(string politica, string esquema, string tabla, IEnumerable<string> acciones) =>
        $"CREATE AUDIT POLICY {Politica(politica)} ACTIONS " + string.Join(", ",
            AccionesValidas(acciones, AccionesObjeto, "de objeto").Select(a => $"{a} ON {Identificador.ValidarCalificado(esquema, tabla)}"));

    public static string CrearSistema(string politica, IEnumerable<string> acciones) =>
        $"CREATE AUDIT POLICY {Politica(politica)} ACTIONS {string.Join(", ", AccionesValidas(acciones, AccionesSistema, "de sistema"))}";

    public static string Activar(string politica, string? usuario, FiltroResultadoAuditoria filtro)
    {
        var sql = $"AUDIT POLICY {Politica(politica)}";
        if (!string.IsNullOrWhiteSpace(usuario))
            sql += $" BY {Identificador.Validar(usuario, "usuario")}";
        return sql + SufijoResultado(filtro);
    }

    public static string ActivarFallosLogon() => "AUDIT POLICY ORA_LOGON_FAILURES WHENEVER NOT SUCCESSFUL";

    public static IReadOnlyList<string> DesactivarTodas(string politica, IEnumerable<AlcancePoliticaAuditoria> alcances)
    {
        var p = Politica(politica);
        var lista = alcances.ToList();
        if (lista.Count == 0) return [];

        if (lista.Any(EsGlobalOExcept))
        {
            if (lista.Any(a => !EsGlobalOExcept(a)))
                throw new InvalidOperationException("La política tiene alcances mezclados no seguros de desactivar automáticamente.");
            return [$"NOAUDIT POLICY {p}"];
        }

        if (lista.All(EsPorUsuario))
            return lista.Select(a => $"NOAUDIT POLICY {p} BY {Identificador.Validar(a.Entidad, "usuario")}").Distinct().ToArray();

        throw new InvalidOperationException("La política tiene un alcance por rol o desconocido. La aplicación no emitirá NOAUDIT para ese alcance.");
    }

    public static string Eliminar(string politica) => $"DROP AUDIT POLICY {Politica(politica)}";

    public static bool PuedeEliminar(string politica, string? oracleSupplied, string? common, string? inherited, bool creadaEnInstancia) =>
        creadaEnInstancia && EsNombrePropio(politica) &&
        !string.Equals(oracleSupplied, "YES", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(common, "NO", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(inherited, "YES", StringComparison.OrdinalIgnoreCase);

    public static bool EsNombrePropio(string politica) =>
        politica.StartsWith("POL_", StringComparison.OrdinalIgnoreCase) && !politica.StartsWith("ORA_", StringComparison.OrdinalIgnoreCase);

    public static bool EsGlobalOExcept(AlcancePoliticaAuditoria alcance) =>
        string.Equals(alcance.Entidad, "ALL USERS", StringComparison.OrdinalIgnoreCase) ||
        Opcion(alcance) is "EXCEPT" or "EXCEPT USER";

    public static bool EsPorUsuario(AlcancePoliticaAuditoria alcance) =>
        Opcion(alcance) is "BY" or "BY USER" &&
        string.Equals(alcance.TipoEntidad, "USER", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(alcance.Entidad, "ALL USERS", StringComparison.OrdinalIgnoreCase);

    private static string Politica(string politica)
    {
        var p = Identificador.Validar(politica, "política");
        if (!EsNombrePropio(p) || p.Length == "POL_".Length)
            throw new ArgumentException("La política debe empezar con POL_ y no puede ser una política de Oracle.");
        return p;
    }

    private static IReadOnlyList<string> AccionesValidas(IEnumerable<string> acciones, IReadOnlyList<string> permitidas, string tipo)
    {
        var lista = acciones.Select(a => (a ?? "").Trim().ToUpperInvariant()).Distinct().ToArray();
        if (lista.Length == 0)
            throw new ArgumentException($"Elija al menos una acción {tipo}.");
        if (lista.FirstOrDefault(a => !permitidas.Contains(a, StringComparer.Ordinal)) is { } invalida)
            throw new ArgumentException($"La acción «{invalida}» no está permitida.");
        return lista;
    }

    private static string SufijoResultado(FiltroResultadoAuditoria filtro) => filtro switch
    {
        FiltroResultadoAuditoria.Exitos => " WHENEVER SUCCESSFUL",
        FiltroResultadoAuditoria.Fallos => " WHENEVER NOT SUCCESSFUL",
        _ => ""
    };

    private static string Opcion(AlcancePoliticaAuditoria alcance) => alcance.Opcion.Trim().ToUpperInvariant();
}

public enum FiltroResultadoAuditoria { Ambos, Exitos, Fallos }

/// <summary>Fila de AUDIT_UNIFIED_ENABLED_POLICIES necesaria para construir NOAUDIT de forma segura.</summary>
public sealed record AlcancePoliticaAuditoria(string Opcion, string Entidad, string TipoEntidad, string Exito, string Fallo);

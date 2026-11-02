using System.Globalization;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>Cómo se fija un límite: un número, UNLIMITED o DEFAULT (el valor del perfil DEFAULT).</summary>
public enum ModoLimite { Valor, Unlimited, Default }

/// <summary>Un límite de perfil editable desde la app.</summary>
public sealed record LimitePerfil(string Recurso, ModoLimite Modo, decimal Numero = 0);

/// <summary>
/// Arma el DDL de perfiles (issue #10). Los nombres pasan por <see cref="Identificador.Validar"/>.
/// PASSWORD_LOCK_TIME se edita en minutos y se guarda en días (n/1440); el resto, en su unidad de Oracle.
/// No se usa PASSWORD_VERIFY_FUNCTION: falla si no se corrió utlpwdmg.sql en la PDB (docs/diseno.md §7).
/// </summary>
public static class DdlPerfiles
{
    /// <summary>Límites que se editan en el diálogo, en este orden, con su unidad y qué significan.</summary>
    public static readonly IReadOnlyList<(string Recurso, string Unidad, string Descripcion)> Editables =
    [
        ("FAILED_LOGIN_ATTEMPTS", "intentos", "Intentos fallidos antes de bloquear la cuenta"),
        ("PASSWORD_LOCK_TIME", "minutos", "Tiempo que la cuenta queda bloqueada"),
        ("PASSWORD_LIFE_TIME", "días", "Días que dura la contraseña"),
        ("PASSWORD_GRACE_TIME", "días", "Días de gracia para cambiarla al vencer"),
        ("PASSWORD_REUSE_MAX", "cambios", "Cambios antes de poder repetir una contraseña"),
        ("SESSIONS_PER_USER", "sesiones", "Sesiones abiertas a la vez por usuario"),
        ("IDLE_TIME", "minutos", "Inactividad antes de cortar la sesión"),
        ("CONNECT_TIME", "minutos", "Duración máxima de una sesión")
    ];

    /// <summary>Qué significa cada recurso de DBA_PROFILES (para la grilla de límites).</summary>
    public static readonly IReadOnlyDictionary<string, string> Descripciones = new Dictionary<string, string>
    {
        ["FAILED_LOGIN_ATTEMPTS"] = "Intentos fallidos antes de bloquear la cuenta",
        ["PASSWORD_LOCK_TIME"] = "Tiempo que la cuenta queda bloqueada",
        ["PASSWORD_LIFE_TIME"] = "Días que dura la contraseña",
        ["PASSWORD_GRACE_TIME"] = "Días de gracia para cambiarla al vencer",
        ["PASSWORD_REUSE_MAX"] = "Cambios antes de poder repetir una contraseña",
        ["PASSWORD_REUSE_TIME"] = "Días antes de poder repetir una contraseña",
        ["PASSWORD_VERIFY_FUNCTION"] = "Función que valida la complejidad de la contraseña",
        ["PASSWORD_ROLLOVER_TIME"] = "Días en que la contraseña vieja y la nueva sirven a la vez",
        ["INACTIVE_ACCOUNT_TIME"] = "Días sin conectarse antes de bloquear la cuenta",
        ["SESSIONS_PER_USER"] = "Sesiones abiertas a la vez por usuario",
        ["IDLE_TIME"] = "Minutos de inactividad antes de cortar la sesión",
        ["CONNECT_TIME"] = "Minutos máximos de una sesión",
        ["CPU_PER_SESSION"] = "CPU por sesión (centésimas de segundo)",
        ["CPU_PER_CALL"] = "CPU por llamada (centésimas de segundo)",
        ["LOGICAL_READS_PER_SESSION"] = "Bloques leídos por sesión",
        ["LOGICAL_READS_PER_CALL"] = "Bloques leídos por llamada",
        ["COMPOSITE_LIMIT"] = "Costo total de la sesión (unidades de servicio)",
        ["PRIVATE_SGA"] = "Memoria privada en la SGA (bytes)"
    };

    /// <summary>DEFAULT y los perfiles de Oracle (ORA_STIG_PROFILE…) no se editan ni se borran desde la app.</summary>
    public static bool EsProtegido(string perfil) =>
        perfil.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) || perfil.StartsWith("ORA_", StringComparison.OrdinalIgnoreCase);

    public static string Crear(string perfil, IEnumerable<LimitePerfil> limites)
    {
        var lista = Limites(limites);
        if (lista.Length == 0) throw new ArgumentException("Defina al menos un límite para el perfil.");
        return $"CREATE PROFILE {Nombre(perfil)} LIMIT {lista}";
    }

    public static string Modificar(string perfil, IEnumerable<LimitePerfil> limites)
    {
        var p = Nombre(perfil);
        if (EsProtegido(p)) throw new ArgumentException($"El perfil {p} es de Oracle o el predeterminado: no se modifica desde la app.");
        var lista = Limites(limites);
        if (lista.Length == 0) throw new ArgumentException("No cambió ningún límite.");
        return $"ALTER PROFILE {p} LIMIT {lista}";
    }

    public static string Eliminar(string perfil)
    {
        var p = Nombre(perfil);
        if (EsProtegido(p)) throw new ArgumentException($"El perfil {p} no se puede eliminar: es de Oracle o el predeterminado.");
        return $"DROP PROFILE {p} CASCADE";
    }

    public static string Asignar(string usuario, string perfil) =>
        $"ALTER USER {Identificador.Validar(usuario, "usuario")} PROFILE {Nombre(perfil)}";

    /// <summary>Valor tal como va en el DDL: UNLIMITED, DEFAULT, un número o n/1440 para PASSWORD_LOCK_TIME.</summary>
    public static string ValorSql(LimitePerfil l) => l.Modo switch
    {
        ModoLimite.Unlimited => "UNLIMITED",
        ModoLimite.Default => "DEFAULT",
        _ when l.Numero <= 0 => throw new ArgumentException($"{l.Recurso} debe ser mayor que cero."),
        _ when l.Recurso == "PASSWORD_LOCK_TIME" => $"{Numero(l.Numero)}/1440",
        _ => Numero(l.Numero)
    };

    /// <summary>
    /// Convierte el LIMIT de DBA_PROFILES a lo que muestra el diálogo. PASSWORD_LOCK_TIME viene en días
    /// (por ejemplo .0006 para 1/1440) y se pasa a minutos. Oracle lo formatea con el separador decimal
    /// de la sesión (",0006" en español), así que se aceptan coma y punto.
    /// </summary>
    public static LimitePerfil Leer(string recurso, string limite)
    {
        var texto = (limite ?? "").Trim().ToUpperInvariant().Replace(',', '.');
        if (texto == "UNLIMITED") return new(recurso, ModoLimite.Unlimited);
        if (texto is "DEFAULT" or "") return new(recurso, ModoLimite.Default);
        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)) return new(recurso, ModoLimite.Default);
        return new(recurso, ModoLimite.Valor, recurso == "PASSWORD_LOCK_TIME" ? Math.Max(1, Math.Round(n * 1440)) : n);
    }

    /// <summary>
    /// Valor para mostrar en la grilla: PASSWORD_LOCK_TIME, que Oracle guarda en días, se muestra en minutos
    /// o días («1 min», «1 día»); el resto queda como lo devuelve DBA_PROFILES.
    /// </summary>
    public static string ValorLegible(string recurso, string limite)
    {
        if (recurso != "PASSWORD_LOCK_TIME") return limite;
        var l = Leer(recurso, limite);
        if (l.Modo != ModoLimite.Valor) return limite;
        return l.Numero < 1440 ? $"{l.Numero:0} min" : $"{l.Numero / 1440:0.##} {(l.Numero == 1440 ? "día" : "días")}";
    }

    private static string Limites(IEnumerable<LimitePerfil> limites) =>
        string.Join(" ", limites.Select(l => $"{Identificador.Validar(l.Recurso, "recurso")} {ValorSql(l)}"));

    private static string Nombre(string perfil) => Identificador.Validar(perfil, "perfil");

    private static string Numero(decimal n) => n.ToString("0.####", CultureInfo.InvariantCulture);
}

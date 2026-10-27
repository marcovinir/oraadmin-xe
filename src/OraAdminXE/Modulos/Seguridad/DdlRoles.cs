using System.Text.RegularExpressions;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>Arma y valida el DDL de roles y privilegios (GRANT / REVOKE).</summary>
public static partial class DdlRoles
{
    /// <summary>Privilegios de objeto que ofrece la pantalla (sobre tablas).</summary>
    public static readonly IReadOnlyList<string> PrivilegiosObjeto = ["SELECT", "INSERT", "UPDATE", "DELETE", "REFERENCES", "ALTER", "INDEX"];

    /// <summary>Privilegios de sistema más usados, primero en la lista.</summary>
    public static readonly IReadOnlyList<string> SistemaComunes =
        ["CREATE SESSION", "CREATE TABLE", "CREATE VIEW", "CREATE SEQUENCE", "CREATE PROCEDURE", "CREATE TRIGGER", "UNLIMITED TABLESPACE"];

    [GeneratedRegex("^[A-Z][A-Z ]{2,60}[A-Z]$")]
    private static partial Regex PatronPrivilegio();

    public static string CrearRol(string rol) => $"CREATE ROLE {Identificador.Validar(rol, "rol")}";
    public static string EliminarRol(string rol) => $"DROP ROLE {Identificador.Validar(rol, "rol")}";

    public static string OtorgarRol(string rol, string beneficiario, bool conAdmin) =>
        $"GRANT {Identificador.Validar(rol, "rol")} TO {Identificador.Validar(beneficiario, "beneficiario")}{(conAdmin ? " WITH ADMIN OPTION" : "")}";

    public static string OtorgarSistema(string privilegio, string beneficiario, bool conAdmin) =>
        $"GRANT {Sistema(privilegio)} TO {Identificador.Validar(beneficiario, "beneficiario")}{(conAdmin ? " WITH ADMIN OPTION" : "")}";

    public static string OtorgarObjeto(IEnumerable<string> privilegios, string esquema, string tabla, string beneficiario, bool conGrant)
    {
        var lista = privilegios.Select(p => p.Trim().ToUpperInvariant()).ToList();
        if (lista.Count == 0)
            throw new ArgumentException("Elija al menos un privilegio (SELECT, INSERT…).");
        if (lista.FirstOrDefault(p => !PrivilegiosObjeto.Contains(p)) is { } malo)
            throw new ArgumentException($"El privilegio de objeto «{malo}» no está permitido.");
        return $"GRANT {string.Join(", ", lista)} ON {Identificador.ValidarCalificado(esquema, tabla)} " +
               $"TO {Identificador.Validar(beneficiario, "beneficiario")}{(conGrant ? " WITH GRANT OPTION" : "")}";
    }

    /// <summary>REVOKE según la fila de la grilla de privilegios (tipo ROL, SISTEMA u OBJETO).</summary>
    public static string Revocar(string tipo, string privilegio, string? objeto, string beneficiario)
    {
        var g = Identificador.Validar(beneficiario, "beneficiario");
        return tipo switch
        {
            "ROL" => $"REVOKE {Identificador.Validar(privilegio, "rol")} FROM {g}",
            "SISTEMA" => $"REVOKE {Sistema(privilegio)} FROM {g}",
            "OBJETO" when objeto?.Split('.') is [var esquema, var tabla] && PrivilegiosObjeto.Contains(privilegio) =>
                $"REVOKE {privilegio} ON {Identificador.ValidarCalificado(esquema, tabla)} FROM {g}",
            _ => throw new ArgumentException($"No se puede revocar «{privilegio}» ({tipo}) desde la app.")
        };
    }

    private static string Sistema(string privilegio)
    {
        var p = Regex.Replace((privilegio ?? "").Trim().ToUpperInvariant(), @"\s+", " ");
        if (!PatronPrivilegio().IsMatch(p))
            throw new ArgumentException($"«{privilegio}» no es un privilegio de sistema válido.");
        return p;
    }
}

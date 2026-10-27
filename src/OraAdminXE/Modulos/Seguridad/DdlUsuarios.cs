using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Arma el DDL de usuarios. Los nombres pasan por <see cref="Identificador.Validar"/> y las contraseñas
/// (que no admiten bind en un DDL) se validan aquí; la bitácora y «Ver SQL» las enmascaran.
/// </summary>
public static class DdlUsuarios
{
    private static readonly HashSet<string> SiempreProtegidos = ["SYS", "SYSTEM", "PDBADMIN"];

    public static IReadOnlyList<string> Crear(string usuario, string clave, string tablespace, int? cuotaMb, string perfil, bool otorgarSesion)
    {
        var u = Identificador.Validar(usuario, "usuario");
        var ts = Identificador.Validar(tablespace, "tablespace");
        var p = Identificador.Validar(perfil, "perfil");
        var sentencias = new List<string>
        {
            $"CREATE USER {u} IDENTIFIED BY {Clave(clave)} DEFAULT TABLESPACE {ts} TEMPORARY TABLESPACE TEMP {Cuota(cuotaMb, ts)} PROFILE {p}"
        };
        if (otorgarSesion)
            sentencias.Add($"GRANT CREATE SESSION TO {u}");
        return sentencias;
    }

    public static string Bloquear(string usuario) => $"ALTER USER {Identificador.Validar(usuario, "usuario")} ACCOUNT LOCK";
    public static string Desbloquear(string usuario) => $"ALTER USER {Identificador.Validar(usuario, "usuario")} ACCOUNT UNLOCK";
    public static string Expirar(string usuario) => $"ALTER USER {Identificador.Validar(usuario, "usuario")} PASSWORD EXPIRE";
    public static string Eliminar(string usuario) => $"DROP USER {Identificador.Validar(usuario, "usuario")} CASCADE";

    public static string CambiarClave(string usuario, string clave) =>
        $"ALTER USER {Identificador.Validar(usuario, "usuario")} IDENTIFIED BY {Clave(clave)}";

    public static string CambiarTablespace(string usuario, string tablespace, int? cuotaMb)
    {
        var ts = Identificador.Validar(tablespace, "tablespace");
        return $"ALTER USER {Identificador.Validar(usuario, "usuario")} DEFAULT TABLESPACE {ts} {Cuota(cuotaMb, ts)}";
    }

    /// <summary>Usuarios que la app no deja modificar: los de Oracle, SYS/SYSTEM y el de la sesión actual.</summary>
    public static bool EsProtegido(string usuario, string oracleMaintained, string usuarioSesion) =>
        oracleMaintained == "Y" || SiempreProtegidos.Contains(usuario) ||
        string.Equals(usuario, usuarioSesion, StringComparison.OrdinalIgnoreCase);

    private static string Cuota(int? mb, string ts) => mb is null ? $"QUOTA UNLIMITED ON {ts}" : $"QUOTA {mb}M ON {ts}";

    private static string Clave(string clave)
    {
        if (string.IsNullOrEmpty(clave))
            throw new ArgumentException("La contraseña no puede estar vacía.");
        if (clave.Contains('"'))
            throw new ArgumentException("La contraseña no puede contener comillas dobles (\").");
        return $"\"{clave}\"";
    }
}

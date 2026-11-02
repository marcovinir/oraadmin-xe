using System.Text.RegularExpressions;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Validaciones y sentencias seguras para objetos DIRECTORY.</summary>
public static partial class DdlDirectorios
{
    /// <summary>Único nombre de demostración administrable sin una columna persistente de proveniencia.</summary>
    public const string DirectorioDemostracion = "DIR_RESPALDOS2";
    public const string RutaRaizRespaldos = @"C:\respaldos_bd2";

    private static readonly HashSet<string> DirectoriosProtegidos =
    [
        "DIR_RESPALDOS", "DATA_PUMP_DIR", "ORACLE_OCM_CONFIG_DIR", "ORACLE_BASE", "ORACLE_HOME", "XMLDIR"
    ];

    private static readonly HashSet<string> UsuariosProtegidos = ["SYS", "SYSTEM"];

    [GeneratedRegex("^[A-Za-z]:[\\\\/]")]
    private static partial Regex RutaLocalWindows();

    public static string Crear(string nombre, string ruta)
    {
        var directorio = Identificador.Validar(nombre, "directorio");
        if (!EsNombreDemostracionPermitido(directorio))
            throw new ArgumentException($"Solo se permite crear el DIRECTORY aislado {DirectorioDemostracion}.");
        return $"CREATE DIRECTORY {directorio} AS {Identificador.Literal(ValidarRutaCreacion(ruta))}";
    }

    public static string OtorgarLecturaEscritura(string directorio, string usuario) =>
        $"GRANT READ, WRITE ON DIRECTORY {ValidarDirectorioDemostracion(directorio)} TO {Identificador.Validar(usuario, "usuario")}";

    public static string Eliminar(string directorio) =>
        $"DROP DIRECTORY {ValidarDirectorioDemostracion(directorio)}";

    /// <summary>SQL de vista con literales escapados; la ejecución real del MERGE usa binds.</summary>
    public static string RegistrarVista(string nombre, string ruta) =>
        SqlDirectorios.Registrar.Replace(":nombre", Identificador.Literal(Identificador.Validar(nombre, "directorio")), StringComparison.Ordinal)
                                .Replace(":ruta", Identificador.Literal(ruta), StringComparison.Ordinal);

    public static string DesactivarVista(int id) =>
        $"UPDATE admin_bd.directorio SET activo = 'N' WHERE id_directorio = {id} AND activo = 'S'";

    public static bool EsNombreReservado(string directorio) =>
        DirectoriosProtegidos.Contains(Identificador.Validar(directorio, "directorio"));

    /// <summary>
    /// Sin proveniencia persistente solo se permite crear el DIRECTORY aislado de la demostración.
    /// Los objetos existentes, aunque se registren, no se consideran propiedad de la aplicación.
    /// </summary>
    public static bool EsNombreDemostracionPermitido(string directorio) =>
        string.Equals(Identificador.Validar(directorio, "directorio"), DirectorioDemostracion, StringComparison.Ordinal);

    private static string ValidarDirectorioDemostracion(string directorio)
    {
        var validado = Identificador.Validar(directorio, "directorio");
        if (!EsNombreDemostracionPermitido(validado))
            throw new ArgumentException($"Solo se permite administrar el DIRECTORY aislado {DirectorioDemostracion}.");
        return validado;
    }

    public static bool EsUsuarioProtegido(string usuario, string oracleMaintained, string usuarioSesion) =>
        oracleMaintained == "Y" || UsuariosProtegidos.Contains(Identificador.Validar(usuario, "usuario")) ||
        string.Equals(usuario, usuarioSesion, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Acepta solo rutas locales Windows plenamente calificadas. No se aceptan UNC, raíces ni rutas
    /// que atraviesen enlaces simbólicos/reparse points ya existentes.
    /// </summary>
    public static string ValidarRutaCreacion(string ruta)
    {
        var original = (ruta ?? "").Trim();
        if (!RutaLocalWindows().IsMatch(original) || original.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("Elija una ruta local absoluta de Windows, por ejemplo C:\\respaldos_bd2. No se permiten rutas UNC ni relativas.");
        if (original.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(p => p is "." or ".."))
            throw new ArgumentException("La ruta no puede contener los segmentos . ni ...");

        string completa;
        try { completa = Path.GetFullPath(original); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("La ruta de carpeta no es válida.", ex);
        }

        var raiz = Path.GetPathRoot(completa) ?? "";
        if (string.Equals(Path.TrimEndingDirectorySeparator(completa), Path.TrimEndingDirectorySeparator(raiz), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("No se permite usar la raíz de una unidad como DIRECTORY.");

        try
        {
            if (new DriveInfo(raiz).DriveType != DriveType.Fixed)
                throw new ArgumentException("La ruta debe estar en un disco local fijo, no en una unidad de red o extraíble.");
        }
        catch (ArgumentException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("No se puede validar la unidad local de la ruta seleccionada.", ex);
        }

        var raizPermitida = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RutaRaizRespaldos));
        var dentroDeRaiz = string.Equals(completa, raizPermitida, StringComparison.OrdinalIgnoreCase) ||
                            completa.StartsWith(raizPermitida + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (!dentroDeRaiz)
            throw new ArgumentException($"La carpeta debe ser {RutaRaizRespaldos} o una subcarpeta segura dentro de esa raíz dedicada de respaldos.");

        for (DirectoryInfo? actual = new DirectoryInfo(completa); actual is not null; actual = actual.Parent)
        {
            if (!actual.Exists) continue;
            try
            {
                if ((actual.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("La ruta no puede estar dentro de un enlace simbólico o reparse point.");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                throw new ArgumentException("No se puede comprobar de forma segura la ruta seleccionada.");
            }
        }
        return Path.TrimEndingDirectorySeparator(completa);
    }

    /// <summary>Estados no eliminados, nulos o desconocidos conservan capacidad histórica y bloquean DROP.</summary>
    public static bool EstadoRespaldoImpideEliminar(string? estado) =>
        !string.Equals(estado, "ELIMINADO", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Dependencias que hacen inseguro desactivar y eliminar un DIRECTORY.</summary>
public sealed record DependenciasEliminacion(
    int RespaldosRestaurables,
    int RestauracionesActivas,
    int ProgramacionesActivas,
    int JobsEjecutando)
{
    public bool ImpideEliminar => RespaldosRestaurables > 0 || RestauracionesActivas > 0 ||
                                  ProgramacionesActivas > 0 || JobsEjecutando > 0;
}

using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>
/// Reglas de seguridad y constructores de DDL para tablespaces de demostración.
/// No ejecuta SQL ni toca el sistema de archivos salvo la validación explícita que
/// pide la UI antes de crear un archivo administrado manualmente.
/// </summary>
public static class DdlTablespaces
{
    private const long Mb = 1024L * 1024L;
    // Límite de control WinForms. El límite real se calcula con BLOCK_SIZE para cada SMALLFILE.
    public const long MaximoMbUi = 1_000_000;
    private const long MaxBloquesSmallfile = 4_194_303;
    private static readonly HashSet<string> CreadosEnSesion = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SiempreProtegidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYSTEM", "SYSAUX", "USERS", "TEMP"
    };

    public static string ValidarNombre(string nombre) => Identificador.Validar(nombre, "tablespace");

    /// <summary>Solo admite el tablespace de la demostración o nombres de prueba deliberados.</summary>
    public static bool EsNombrePruebaAutorizado(string nombre)
    {
        try
        {
            var limpio = ValidarNombre(nombre);
            return limpio == "TS_VENTAS" || (limpio.StartsWith("ORAADMIN_TEST_", StringComparison.Ordinal) && limpio.Length > "ORAADMIN_TEST_".Length);
        }
        catch (ArgumentException) { return false; }
    }

    public static bool EsProtegido(string nombre, string contenido) =>
        SiempreProtegidos.Contains(nombre) ||
        string.Equals(contenido, "UNDO", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(contenido, "TEMPORARY", StringComparison.OrdinalIgnoreCase) ||
        nombre.StartsWith("UNDOTBS", StringComparison.OrdinalIgnoreCase);

    /// <summary>Política de la app: no administra tablespaces de aplicación no verificables.</summary>
    public static bool EsElegibleParaAdministrar(string nombre, string contenido) =>
        string.Equals(contenido, "PERMANENT", StringComparison.OrdinalIgnoreCase) &&
        !EsProtegido(nombre, contenido) && EsNombrePruebaAutorizado(nombre);

    public static bool PuedeAdministrar(string nombre, string contenido) =>
        EsElegibleParaAdministrar(nombre, contenido) && CreadosEnSesion.Contains(ValidarNombre(nombre));

    public static bool PuedeEliminar(string nombre, string contenido, long? segmentos, long? usuariosAsignados,
        long? objetosAsignados = 0, long? cuotas = 0, long? predeterminadosPdb = 0) =>
        EsElegibleParaAdministrar(nombre, contenido) && segmentos == 0 && usuariosAsignados == 0 && objetosAsignados == 0 &&
        cuotas == 0 && predeterminadosPdb == 0 && CreadosEnSesion.Contains(ValidarNombre(nombre));

    public static void RegistrarCreadoEnSesion(string nombre) => CreadosEnSesion.Add(ValidarNombre(nombre));
    public static void OlvidarCreadoEnSesion(string nombre) => CreadosEnSesion.Remove(ValidarNombre(nombre));
    public static bool FueCreadoEnSesion(string nombre) => CreadosEnSesion.Contains(ValidarNombre(nombre));

    public static bool TransicionValida(string estadoActual, EstadoTablespace destino) => (estadoActual.ToUpperInvariant(), destino) switch
    {
        ("ONLINE", EstadoTablespace.Offline) => true,
        ("OFFLINE", EstadoTablespace.Online) => true,
        ("ONLINE", EstadoTablespace.SoloLectura) => true,
        ("READ ONLY", EstadoTablespace.LecturaEscritura) => true,
        _ => false
    };

    public static string Crear(string nombre, string ruta, long inicialMb, bool autoextend, long? nextMb, long? maxMb,
        long maximoMb = MaximoMbUi)
    {
        var ts = ValidarNombrePrueba(nombre);
        var archivo = ValidarRutaDdl(ruta);
        ValidarNombreArchivo(ts, archivo);
        ValidarTamano(inicialMb, "tamaño inicial", maximoMb);
        var extension = ClausulaAutoextend(autoextend, inicialMb, nextMb, maxMb, maximoMb);
        return $"CREATE TABLESPACE {ts} DATAFILE {Identificador.Literal(archivo)} SIZE {inicialMb}M{extension}";
    }

    public static string AgregarDatafile(string nombre, string ruta, long tamanoMb, long maximoMb = MaximoMbUi)
    {
        var ts = ValidarNombrePrueba(nombre);
        var archivo = ValidarRutaDdl(ruta);
        ValidarNombreArchivo(ts, archivo);
        ValidarTamano(tamanoMb, "tamaño del datafile", maximoMb);
        return $"ALTER TABLESPACE {ts} ADD DATAFILE {Identificador.Literal(archivo)} SIZE {tamanoMb}M";
    }

    public static string RedimensionarDatafile(string ruta, long tamanoMb, long maximoMb = MaximoMbUi)
    {
        var archivo = ValidarRutaDdl(ruta);
        ValidarTamano(tamanoMb, "nuevo tamaño", maximoMb);
        return $"ALTER DATABASE DATAFILE {Identificador.Literal(archivo)} RESIZE {tamanoMb}M";
    }

    public static string CambiarAutoextend(string ruta, bool activar, long tamanoActualMb, long? nextMb = null, long? maxMb = null,
        long maximoPermitidoMb = MaximoMbUi)
    {
        var archivo = ValidarRutaDdl(ruta);
        ValidarTamano(tamanoActualMb, "tamaño actual", maximoPermitidoMb);
        var clausula = ClausulaAutoextend(activar, tamanoActualMb, nextMb, maxMb, maximoPermitidoMb).TrimStart();
        return $"ALTER DATABASE DATAFILE {Identificador.Literal(archivo)} {clausula}";
    }

    public static string CambiarEstado(string nombre, EstadoTablespace destino)
    {
        var ts = ValidarNombrePrueba(nombre);
        var clausula = destino switch
        {
            EstadoTablespace.Online => "ONLINE",
            EstadoTablespace.Offline => "OFFLINE NORMAL",
            EstadoTablespace.SoloLectura => "READ ONLY",
            EstadoTablespace.LecturaEscritura => "READ WRITE",
            _ => throw new ArgumentOutOfRangeException(nameof(destino))
        };
        return $"ALTER TABLESPACE {ts} {clausula}";
    }

    public static string Eliminar(string nombre) => $"DROP TABLESPACE {ValidarNombrePrueba(nombre)} INCLUDING CONTENTS AND DATAFILES";

    public static string ValidarNombrePrueba(string nombre)
    {
        var validado = ValidarNombre(nombre);
        if (!EsNombrePruebaAutorizado(validado))
            throw new ArgumentException("Solo se permiten TS_VENTAS u ORAADMIN_TEST_* para administración de prueba.");
        return validado;
    }

    public static long MaximoMbSmallfile(long blockSize)
    {
        if (blockSize is < 512 or > 32_768 || (blockSize & (blockSize - 1)) != 0)
            throw new ArgumentException("BLOCK_SIZE no es válido para un datafile SMALLFILE.", nameof(blockSize));
        return checked(MaxBloquesSmallfile * blockSize / Mb);
    }

    public static void ValidarTamano(long mb, string campo, long maximoMb = MaximoMbUi)
    {
        if (maximoMb < 1 || mb is < 1 || mb > maximoMb)
            throw new ArgumentException($"El {campo} debe estar entre 1 y {maximoMb:N0} MB.");
    }

    public static void ValidarAutoextend(long tamanoActualMb, long? nextMb, long? maxMb, long maximoPermitidoMb = MaximoMbUi)
    {
        ValidarTamano(tamanoActualMb, "tamaño actual", maximoPermitidoMb);
        if (nextMb is null || maxMb is null)
            throw new ArgumentException("Indique NEXT y MAXSIZE cuando autoextend está activado.");
        ValidarTamano(nextMb.Value, "NEXT", maximoPermitidoMb);
        ValidarTamano(maxMb.Value, "MAXSIZE", maximoPermitidoMb);
        if (maxMb < tamanoActualMb + nextMb)
            throw new ArgumentException("MAXSIZE debe permitir al menos el tamaño actual más un incremento NEXT.");
    }

    /// <summary>
    /// Valida una ruta Windows administrada manualmente bajo el directorio que Oracle ya usa
    /// para USERS. La UI pasa <paramref name="verificarSistemaArchivos"/> en true para rechazar
    /// rutas inaccesibles, reparse points y archivos físicos existentes.
    /// </summary>
    public static string ValidarRutaDatafile(string ruta, string directorioPermitido, bool verificarSistemaArchivos)
    {
        var (archivo, _) = ValidarRutaDatafileComun(ruta, directorioPermitido, verificarSistemaArchivos);
        if (verificarSistemaArchivos && File.Exists(archivo))
            throw new ArgumentException("Ya existe un archivo físico con ese nombre; elija otro nombre.");
        return archivo;
    }

    /// <summary>Valida un DBF existente sin tratar su existencia como una colisión.</summary>
    public static string ValidarRutaDatafileExistente(string ruta, string directorioPermitido, bool verificarSistemaArchivos)
    {
        var (archivo, _) = ValidarRutaDatafileComun(ruta, directorioPermitido, verificarSistemaArchivos);
        if (verificarSistemaArchivos)
        {
            if (!File.Exists(archivo)) throw new ArgumentException("El datafile existente no está disponible localmente para validación.");
            if ((File.GetAttributes(archivo) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("No se permiten datafiles que sean reparse points o enlaces simbólicos.");
        }
        return archivo;
    }

    public static bool RutaEstaRegistrada(string ruta, IEnumerable<string> rutasRegistradas) =>
        rutasRegistradas.Any(r => string.Equals(r, ruta, StringComparison.OrdinalIgnoreCase));

    public static string DirectorioDeDatafile(string ruta)
    {
        var normalizada = NormalizarRutaWindows(ruta, "ruta del datafile");
        return normalizada[..normalizada.LastIndexOf('\\')];
    }

    public static string SugerirRutaDatafile(string directorio, string tablespace, int numero)
    {
        if (numero is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(numero));
        var raiz = NormalizarRutaWindows(directorio, "directorio de datafiles").TrimEnd('\\');
        return $"{raiz}\\{ValidarNombre(tablespace)}{numero:00}.DBF";
    }

    private static string ClausulaAutoextend(bool activar, long tamanoActualMb, long? nextMb, long? maxMb, long maximoPermitidoMb)
    {
        if (!activar) return " AUTOEXTEND OFF";
        ValidarAutoextend(tamanoActualMb, nextMb, maxMb, maximoPermitidoMb);
        return $" AUTOEXTEND ON NEXT {nextMb}M MAXSIZE {maxMb}M";
    }

    private static string ValidarRutaDdl(string ruta)
    {
        var archivo = NormalizarRutaWindows(ruta, "ruta del datafile");
        if (!archivo.EndsWith(".DBF", StringComparison.OrdinalIgnoreCase) || EsRutaRestringida(archivo))
            throw new ArgumentException("La ruta del datafile no tiene una extensión .DBF permitida.");
        return archivo;
    }

    private static void ValidarNombreArchivo(string tablespace, string ruta)
    {
        var archivo = ruta[(ruta.LastIndexOf('\\') + 1)..];
        if (!archivo.StartsWith(tablespace, StringComparison.OrdinalIgnoreCase) || !archivo.EndsWith(".DBF", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El nombre del datafile debe empezar con el nombre del tablespace y terminar en .DBF.");
        var sufijo = archivo[tablespace.Length..^4];
        if (sufijo.Length != 2 || !int.TryParse(sufijo, out var numero) || numero is < 1 or > 99)
            throw new ArgumentException("El datafile debe usar un sufijo numérico de 01 a 99.");
    }

    private static string NormalizarRutaWindows(string ruta, string campo)
    {
        var texto = (ruta ?? "").Trim().Replace('/', '\\');
        if (texto.Length < 4 || !char.IsAsciiLetter(texto[0]) || texto[1] != ':' || texto[2] != '\\')
            throw new ArgumentException($"La {campo} debe ser una ruta absoluta Windows en una unidad local.");
        if (texto.StartsWith("\\\\", StringComparison.Ordinal) || texto.Contains('\0') || texto.Contains('\r') || texto.Contains('\n') ||
            texto.IndexOfAny(['"', '\'', '<', '>', '|', '?', '*']) >= 0)
            throw new ArgumentException($"La {campo} contiene caracteres no permitidos.");
        var partes = texto.Split('\\', StringSplitOptions.None);
        if (partes.Any(p => p is "." or "..") || partes.Skip(1).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"La {campo} no puede contener rutas relativas ni segmentos vacíos.");
        return texto.TrimEnd('\\');
    }

    private static bool EsRutaRestringida(string ruta)
    {
        var mayuscula = ruta.ToUpperInvariant();
        return mayuscula.Contains("\\WINDOWS\\", StringComparison.Ordinal);
    }

    private static (string Archivo, string Directorio) ValidarRutaDatafileComun(string ruta, string directorioPermitido, bool verificarSistemaArchivos)
    {
        var archivo = NormalizarRutaWindows(ruta, "ruta del datafile");
        var directorio = NormalizarRutaWindows(directorioPermitido, "directorio de datafiles").TrimEnd('\\');
        var directorioArchivo = archivo[..archivo.LastIndexOf('\\')];
        if (!string.Equals(directorioArchivo, directorio, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El datafile debe ubicarse exactamente en el directorio verificado de USERS.");
        if (!archivo.EndsWith(".DBF", StringComparison.OrdinalIgnoreCase) || EsRutaRestringida(archivo))
            throw new ArgumentException("La ruta del datafile no está permitida.");
        if (!verificarSistemaArchivos) return (archivo, directorio);
        if (!Directory.Exists(directorio))
            throw new ArgumentException("El directorio verificado de Oracle no está disponible desde esta aplicación.");
        for (DirectoryInfo? info = new(directorio); info is not null; info = info.Parent)
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                throw new ArgumentException("No se permiten directorios con reparse point o enlace simbólico.");
        var unidad = new DriveInfo(directorio[..3]);
        if (unidad.DriveType != DriveType.Fixed)
            throw new ArgumentException("Solo se permiten unidades locales fijas; no UNC ni unidades de red.");
        return (archivo, directorio);
    }

    /// <summary>Orquesta la revalidación final, después del preview aprobado y antes del DDL.</summary>
    public static async Task EjecutarTrasRevalidarAsync(Func<Task> revalidar, Func<Task> ejecutar)
    {
        ArgumentNullException.ThrowIfNull(revalidar);
        ArgumentNullException.ThrowIfNull(ejecutar);
        await revalidar();
        await ejecutar();
    }
}

public enum EstadoTablespace { Online, Offline, SoloLectura, LecturaEscritura }

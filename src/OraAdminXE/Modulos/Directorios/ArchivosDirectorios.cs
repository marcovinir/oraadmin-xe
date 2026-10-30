using System.Text;
using System.Text.RegularExpressions;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Reglas puras para listar archivos de respaldo sin abrir la superficie de archivos genéricos.</summary>
public static partial class ArchivosDirectorios
{
    public const int MaxBytesLog = 1024 * 1024;

    private static readonly HashSet<string> NombresReservadosWindows = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
      "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    // Sin proveniencia persistente no se habilitan mutaciones en DIRECTORY externos registrados.
    private static readonly HashSet<string> DirectoriosGestionados = new(StringComparer.OrdinalIgnoreCase)
    { "DIR_RESPALDOS", DdlDirectorios.DirectorioDemostracion };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9 _.$#()\\-]{0,180}\\.dmp$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NombreDumpSeguro();

    public static bool EsExtensionPermitida(string nombre) =>
        string.Equals(Path.GetExtension(nombre), ".dmp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(nombre), ".log", StringComparison.OrdinalIgnoreCase);

    public static bool EsDirectorioGestionado(string nombreOracle) => DirectoriosGestionados.Contains(nombreOracle ?? "");

    public static bool EsDump(string nombre) => string.Equals(Path.GetExtension(nombre), ".dmp", StringComparison.OrdinalIgnoreCase);
    public static bool EsLog(string nombre) => string.Equals(Path.GetExtension(nombre), ".log", StringComparison.OrdinalIgnoreCase);

    public static bool EsBasenameSeguro(string nombre, string extension) =>
        !string.IsNullOrWhiteSpace(nombre) && string.Equals(nombre, Path.GetFileName(nombre), StringComparison.Ordinal) &&
        string.Equals(Path.GetExtension(nombre), extension, StringComparison.OrdinalIgnoreCase) &&
        nombre.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) < 0 && !nombre.Contains("..", StringComparison.Ordinal) &&
        !nombre.Any(char.IsControl);

    /// <summary>Valida un destino DMP como basename Windows, no como ruta.</summary>
    public static string ValidarNombreDump(string nombre)
    {
        var limpio = (nombre ?? "").Trim();
        if (!EsBasenameSeguro(limpio, ".dmp") || !NombreDumpSeguro().IsMatch(limpio))
            throw new ArgumentException("El nuevo nombre debe ser un archivo .dmp simple y seguro, sin rutas ni caracteres especiales.");

        var baseName = Path.GetFileNameWithoutExtension(limpio);
        if (baseName.EndsWith(' ') || baseName.EndsWith('.') || NombresReservadosWindows.Contains(baseName))
            throw new ArgumentException("El nuevo nombre .dmp no es válido para Windows.");
        return limpio;
    }

    /// <summary>Normaliza una ruta local segura para comparar catálogo y diccionario.</summary>
    public static string ValidarRutaLectura(string ruta)
    {
        var original = (ruta ?? "").Trim();
        if (original.Length == 0 || original.StartsWith("\\\\", StringComparison.Ordinal) || !Path.IsPathFullyQualified(original))
            throw new ArgumentException("El DIRECTORY debe apuntar a una ruta local absoluta, no UNC.");
        if (original.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(s => s is "." or ".."))
            throw new ArgumentException("La ruta del DIRECTORY no puede contener segmentos relativos.");

        string completa;
        try { completa = Path.TrimEndingDirectorySeparator(Path.GetFullPath(original)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("La ruta del DIRECTORY no es válida.", ex);
        }

        var raiz = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(completa) ?? "");
        if (string.Equals(completa, raiz, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("No se permite inspeccionar la raíz de una unidad.");
        if (!Path.IsPathFullyQualified(completa) || completa.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("La ruta del DIRECTORY debe ser local y plenamente calificada.");

        try
        {
            if (new DriveInfo(Path.GetPathRoot(completa)!).DriveType != DriveType.Fixed)
                throw new ArgumentException("El DIRECTORY debe estar en una unidad local fija.");
        }
        catch (ArgumentException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("No se pudo validar la unidad del DIRECTORY.", ex);
        }

        for (DirectoryInfo? actual = new(completa); actual is not null; actual = actual.Parent)
        {
            if (!actual.Exists) continue;
            try
            {
                if ((actual.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("El DIRECTORY no puede estar en un enlace simbólico o reparse point.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ArgumentException("No se pudo comprobar de forma segura la ruta del DIRECTORY.", ex);
            }
        }
        return completa;
    }

    public static bool RutasCoinciden(string rutaCatalogo, string rutaOracle)
    {
        try
        {
            return string.Equals(ValidarRutaLectura(rutaCatalogo), ValidarRutaLectura(rutaOracle), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
    }

    public static bool CoincideNombre(string esperado, string encontrado) =>
        string.Equals(Path.GetFileName(esperado), Path.GetFileName(encontrado), StringComparison.OrdinalIgnoreCase);

    public static bool CoincideAsociacion(int idDirectorioFisico, int idDirectorioCatalogo, string esperado, string encontrado) =>
        idDirectorioFisico == idDirectorioCatalogo && CoincideNombre(esperado, encontrado);

    public static bool PuedeMutarDump(string? estadoRespaldo, bool jobDataPumpActivo, bool jobSchedulerActivo, bool restauracionActiva) =>
        string.Equals(estadoRespaldo, "COMPLETADO", StringComparison.OrdinalIgnoreCase) &&
        !jobDataPumpActivo && !jobSchedulerActivo && !restauracionActiva;

    public static bool PuedeReconciliarAusente(EstadoInspeccionDirectorio inspeccion, string? estadoRespaldo, bool archivoPresente,
        bool jobDataPumpActivo, bool jobSchedulerActivo, bool restauracionActiva) =>
        inspeccion == EstadoInspeccionDirectorio.Correcto && !archivoPresente &&
        PuedeMutarDump(estadoRespaldo, jobDataPumpActivo, jobSchedulerActivo, restauracionActiva);

    /// <summary>Decodifica como texto, sin lanzar por bytes inválidos y sin exceder el límite del visor.</summary>
    public static LogLeido DecodificarLog(ReadOnlySpan<byte> bytes, bool truncado)
    {
        Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        var desplazamiento = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { desplazamiento = 3; }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = Encoding.Unicode; desplazamiento = 2; }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = Encoding.BigEndianUnicode; desplazamiento = 2; }
        return new LogLeido(encoding.GetString(bytes[desplazamiento..]), truncado);
    }

    /// <summary>Lee hasta un MiB; el llamador valida previamente DIRECTORY y basename.</summary>
    public static async Task<LogLeido> LeerLogLimitadoAsync(string ruta)
    {
        await using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var longitudInicial = flujo.Length;
        var limite = (int)Math.Min(longitudInicial, MaxBytesLog);
        var buffer = new byte[limite];
        var leidos = 0;
        while (leidos < buffer.Length)
        {
            var n = await flujo.ReadAsync(buffer.AsMemory(leidos, buffer.Length - leidos));
            if (n == 0) break;
            leidos += n;
        }
        return DecodificarLog(buffer.AsSpan(0, leidos), longitudInicial > MaxBytesLog || flujo.Length > leidos);
    }
}

public sealed record LogLeido(string Contenido, bool Truncado);

public enum EstadoInspeccionDirectorio { Correcto, Inexistente, AccesoDenegado, Inseguro, ErrorIo }

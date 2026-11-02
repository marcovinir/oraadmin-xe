using System.Text.RegularExpressions;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Lee el .LOG de Data Pump. Cada tabla exportada o importada deja una línea
/// <c>. . "ESQUEMA"."TABLA"   4.084 MB  200000 filas exportadas</c> (con <c>:"PARTICION"</c> si es particionada).
/// El formato no depende del idioma, así que sirve para saber qué tablas trae un respaldo aunque ya no existan en la base.
/// </summary>
public static partial class LogDataPump
{
    [GeneratedRegex(@"^\. \. ""([^""]+)""\.""([^""]+)""(?::""[^""]+"")?\s+[\d.,]+\s+\w+\s+(\d+)", RegexOptions.Multiline)]
    private static partial Regex LineaTabla();

    /// <summary>Tablas del log (sin repetir las particiones), con las filas que reporta Data Pump.</summary>
    public static IReadOnlyList<(string Esquema, string Tabla, long Filas)> Tablas(string contenido) =>
        LineaTabla().Matches(contenido)
            .GroupBy(m => (m.Groups[1].Value, m.Groups[2].Value))
            .Select(g => (g.Key.Item1, g.Key.Item2, g.Sum(m => long.Parse(m.Groups[3].Value))))
            .ToList();

    /// <summary>
    /// Tablas que el import se saltó porque ya existían (TABLE_EXISTS_ACTION = SKIP). Oracle 21c no lo marca con ORA-,
    /// solo escribe una línea que menciona table_exists_action (en cualquier idioma).
    /// </summary>
    public static int Omitidas(string contenido) =>
        contenido.Split('\n').Count(l => l.Contains("table_exists_action", StringComparison.OrdinalIgnoreCase));

    /// <summary>Lee el log del disco; si ya no existe devuelve una lista vacía.</summary>
    public static IReadOnlyList<(string Esquema, string Tabla, long Filas)> TablasDeArchivo(string ruta)
    {
        try { return Tablas(File.ReadAllText(ruta)); }
        catch (IOException) { return []; }
    }
}

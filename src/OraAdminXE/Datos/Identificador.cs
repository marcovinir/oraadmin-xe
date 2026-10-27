using System.Text.RegularExpressions;

namespace OraAdminXE.Datos;

/// <summary>
/// Los nombres de objetos (tablespaces, usuarios, roles, tablas…) no se pueden pasar como variables bind,
/// así que se validan aquí antes de armar el DDL. Equivale a DBMS_ASSERT.SIMPLE_SQL_NAME para nombres
/// sin comillas. Los VALORES (fechas, filtros, contraseñas en consultas) siempre van con bind (:nombre).
/// </summary>
public static partial class Identificador
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_$#]{0,127}$")]
    private static partial Regex Patron();

    /// <summary>Devuelve el nombre en mayúsculas o lanza <see cref="ArgumentException"/> si no es válido.</summary>
    public static string Validar(string nombre, string queEs = "nombre")
    {
        var limpio = (nombre ?? "").Trim();
        if (!Patron().IsMatch(limpio))
            throw new ArgumentException(
                $"El {queEs} «{nombre}» no es válido: debe empezar con una letra y usar solo letras, números, _, $ o # (máx. 128).");
        return limpio.ToUpperInvariant();
    }

    /// <summary>Valida un nombre calificado ESQUEMA.OBJETO y lo devuelve en mayúsculas.</summary>
    public static string ValidarCalificado(string esquema, string objeto) =>
        $"{Validar(esquema, "esquema")}.{Validar(objeto, "objeto")}";

    /// <summary>Escapa un texto para usarlo dentro de un literal SQL entre comillas simples (rutas de archivos).</summary>
    public static string Literal(string texto) => "'" + (texto ?? "").Replace("'", "''") + "'";
}

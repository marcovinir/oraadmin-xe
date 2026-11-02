using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Una sentencia lista para ejecutar: <see cref="Sql"/> con variables bind, <see cref="Visible"/> con los valores
/// ya escritos (es lo que se enseña en el diálogo de confirmación) y los <see cref="Parametros"/> del bind.
/// </summary>
public sealed record Sentencia(string Sql, string Visible, OracleParameter[] Parametros);

/// <summary>
/// Arma las sentencias de estadísticas e índices (issue #22).
/// Los nombres de objetos en DDL pasan por <see cref="Identificador.Validar"/>; en DBMS_STATS van como bind
/// porque son parámetros del paquete, y se usan tal cual vienen del diccionario.
/// </summary>
public static class DdlTuning
{
    // ------------------------------------------------------------------ estadísticas

    public static Sentencia RecolectarTabla(string esquema, string tabla) => new(
        "BEGIN DBMS_STATS.GATHER_TABLE_STATS(:e, :t, cascade => TRUE); END;",
        $"BEGIN DBMS_STATS.GATHER_TABLE_STATS({Identificador.Literal(esquema)}, {Identificador.Literal(tabla)}, cascade => TRUE); END;",
        [Parametro.De("e", esquema), Parametro.De("t", tabla)]);

    public static Sentencia RecolectarEsquema(string esquema) => new(
        "BEGIN DBMS_STATS.GATHER_SCHEMA_STATS(:e); END;",
        $"BEGIN DBMS_STATS.GATHER_SCHEMA_STATS({Identificador.Literal(esquema)}); END;",
        [Parametro.De("e", esquema)]);

    public static Sentencia BorrarTabla(string esquema, string tabla) => new(
        "BEGIN DBMS_STATS.DELETE_TABLE_STATS(:e, :t); END;",
        $"BEGIN DBMS_STATS.DELETE_TABLE_STATS({Identificador.Literal(esquema)}, {Identificador.Literal(tabla)}); END;",
        [Parametro.De("e", esquema), Parametro.De("t", tabla)]);

    // ------------------------------------------------------------------ índices

    /// <summary>CREATE [UNIQUE] INDEX esquema.indice ON esquema.tabla(col1, col2…), con las columnas en el orden dado.</summary>
    public static string CrearIndice(string esquema, string indice, string tabla, IEnumerable<string> columnas, bool unico)
    {
        var e = Identificador.Validar(esquema, "esquema");
        var ix = Identificador.Validar(indice, "índice");
        var t = Identificador.Validar(tabla, "tabla");
        var cols = columnas.Select(c => Identificador.Validar(c, "columna")).ToList();
        if (cols.Count == 0)
            throw new ArgumentException("Marque al menos una columna para el índice.");
        if (cols.Distinct().Count() != cols.Count)
            throw new ArgumentException("Una columna no puede repetirse en el índice.");
        return $"CREATE {(unico ? "UNIQUE " : "")}INDEX {e}.{ix} ON {e}.{t}({string.Join(", ", cols)})";
    }

    public static string ReconstruirIndice(string esquema, string indice) =>
        $"ALTER INDEX {CalificarIndice(esquema, indice)} REBUILD";

    public static string EliminarIndice(string esquema, string indice) =>
        $"DROP INDEX {CalificarIndice(esquema, indice)}";

    private static string CalificarIndice(string esquema, string indice)
    {
        var e = Identificador.Validar(esquema, "esquema");
        var ix = (indice ?? "").Trim();
        if (ix.StartsWith('"') && ix.EndsWith('"') && ix.Length >= 2)
        {
            var interior = ix[1..^1];
            if (interior.Length == 0 || interior.Length > 128 || interior.Contains('\0') || interior.Replace("\"\"", "").Contains('"'))
                throw new ArgumentException("El índice no es válido: comillas no balanceadas o caracteres inválidos.");
            return $"{e}.{ix}";
        }
        return $"{e}.{Identificador.Validar(ix, "índice")}";
    }

    /// <summary>Nombre sugerido IX_{TABLA}_{COL}[_{COL}…], recortado a los 128 caracteres de Oracle.</summary>
    public static string NombreSugerido(string tabla, IEnumerable<string> columnas)
    {
        var partes = new[] { "IX", tabla }.Concat(columnas).Where(p => !string.IsNullOrWhiteSpace(p));
        var nombre = string.Join("_", partes).ToUpperInvariant();
        return nombre.Length <= 128 ? nombre : nombre[..128];
    }
}

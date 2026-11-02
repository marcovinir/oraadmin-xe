using System.Data;
using System.Globalization;
using System.Text;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Prepara una copia segura para hojas de cálculo; Csv solo serializa y no neutraliza fórmulas.</summary>
public static class ExportacionPistaAuditoria
{
    private static readonly (string Origen, string Encabezado)[] Columnas =
    [
        ("EVENT_TIMESTAMP", "Fecha (BD)"), ("DBUSERNAME", "Usuario BD"), ("OS_USERNAME", "Usuario SO"),
        ("USERHOST", "Host"), ("ACTION_NAME", "Acción"), ("OBJECT_SCHEMA", "Esquema"),
        ("OBJECT_NAME", "Objeto"), ("RETURN_CODE", "Código retorno"), ("UNIFIED_AUDIT_POLICIES", "Políticas"),
        ("SQL_TEXT_PREVIEW", "SQL (vista previa)")
    ];

    public static DataTable Preparar(DataTable origen)
    {
        ArgumentNullException.ThrowIfNull(origen);
        var destino = new DataTable();
        foreach (var (_, encabezado) in Columnas) destino.Columns.Add(encabezado, typeof(string));
        foreach (DataRow fila in origen.Rows)
        {
            var nueva = destino.NewRow();
            for (var i = 0; i < Columnas.Length; i++)
                nueva[i] = Sanitizar(Formatear(Obtener(fila, Columnas[i].Origen)));
            destino.Rows.Add(nueva);
        }
        return destino;
    }

    /// <summary>
    /// Conserva el texto y lo fuerza a literal cuando una hoja podría interpretarlo como fórmula, incluso tras ignorar espacios o controles iniciales.
    /// Ningún CSV puede garantizar seguridad frente a todos los programas, configuraciones o reexportaciones de hojas de cálculo.
    /// </summary>
    public static string Sanitizar(string texto)
    {
        var limpio = QuitarControlesInvalidos(texto);
        if (limpio.Length == 0) return limpio;
        // Primero se quitan controles inválidos: así \0=1 no puede eludir la detección de fórmulas.
        var normalizado = limpio.Normalize(NormalizationForm.FormKC);
        var indice = 0;
        while (indice < normalizado.Length && EsPrefijoIgnorable(normalizado[indice])) indice++;
        if (indice >= normalizado.Length) return limpio;
        var inicial = normalizado[indice];
        return inicial is '=' or '+' or '-' or '@' or '\u2212' ? "'" + limpio : limpio;
    }

    private static object? Obtener(DataRow fila, string columna) => fila.Table.Columns.Contains(columna) ? fila[columna] : null;

    private static string Formatear(object? valor) => valor switch
    {
        null or DBNull => "",
        DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? ""
    };

    private static string QuitarControlesInvalidos(string texto)
    {
        if (!texto.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')) return texto;
        var resultado = new StringBuilder(texto.Length);
        foreach (var caracter in texto)
            if (!char.IsControl(caracter) || caracter is '\r' or '\n' or '\t') resultado.Append(caracter);
        return resultado.ToString();
    }

    private static bool EsPrefijoIgnorable(char caracter) => char.IsWhiteSpace(caracter) || char.IsControl(caracter) ||
        char.GetUnicodeCategory(caracter) is UnicodeCategory.Format or UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}

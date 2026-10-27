using System.Data;
using System.Globalization;
using System.Text;

namespace OraAdminXE.UI;

/// <summary>Convierte una grilla de resultados a CSV que Excel abre bien en español (separador «;»).</summary>
public static class Csv
{
    private const char Separador = ';';

    public static string Generar(DataTable tabla)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(Separador, tabla.Columns.Cast<DataColumn>().Select(c => Escapar(c.ColumnName)))).Append("\r\n");
        foreach (DataRow fila in tabla.Rows)
            sb.Append(string.Join(Separador, fila.ItemArray.Select(v => Escapar(Formatear(v))))).Append("\r\n");
        return sb.ToString();
    }

    private static string Formatear(object? valor) => valor switch
    {
        null or DBNull => "",
        DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable numero => numero.ToString(null, CultureInfo.CurrentCulture),
        _ => valor.ToString() ?? ""
    };

    private static string Escapar(string texto) =>
        texto.IndexOfAny([Separador, '"', '\n', '\r']) >= 0
            ? "\"" + texto.Replace("\"", "\"\"") + "\""
            : texto;
}

using System.Data;
using OraAdminXE.UI;

namespace OraAdminXE.Tests;

public class CsvTests
{
    [Fact]
    public void Generar_usa_punto_y_coma_y_escapa_comillas_y_saltos()
    {
        var tabla = new DataTable();
        tabla.Columns.Add("USUARIO");
        tabla.Columns.Add("ACCION");
        tabla.Columns.Add("FECHA", typeof(DateTime));
        tabla.Rows.Add("JUAN", "Crear \"tabla\"; prueba", new DateTime(2026, 10, 27, 15, 30, 0));
        tabla.Rows.Add("ANA", "línea 1\nlínea 2", DBNull.Value);

        var csv = Csv.Generar(tabla);

        Assert.Equal(
            "USUARIO;ACCION;FECHA\r\n" +
            "JUAN;\"Crear \"\"tabla\"\"; prueba\";2026-10-27 15:30:00\r\n" +
            "ANA;\"línea 1\nlínea 2\";\r\n",
            csv);
    }
}

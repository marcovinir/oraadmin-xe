using System.Data;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.UI;

/// <summary>
/// Diálogos comunes. Todas las pantallas los usan para que el comportamiento sea igual en toda la app.
/// </summary>
public static class Dialogos
{
    /// <summary>Qué hacer ante los errores más comunes (se agrega debajo del mensaje de Oracle).</summary>
    private static readonly Dictionary<int, string> Pistas = new()
    {
        [1017]  = "Revise el usuario y la contraseña.",
        [28000] = "La cuenta está bloqueada. Desbloquéela desde Seguridad o con ALTER USER … ACCOUNT UNLOCK.",
        [12541] = "El listener no está corriendo: inicie el servicio OracleOraDB21Home1TNSListener.",
        [12514] = "El servicio no existe en el listener. En Oracle 21c XE use XEPDB1 (no XE).",
        [12545] = "No se encuentra el servidor: revise el host.",
        [1031]  = "Privilegios insuficientes: use un usuario con el rol DBA.",
        [39070] = "La carpeta del directorio no existe o Oracle no puede escribir en ella.",
        [29283] = "La carpeta del directorio no existe o Oracle no puede escribir en ella."
    };

    /// <summary>
    /// Muestra la sentencia exacta (fuente monoespaciada, con botón Copiar) y pregunta si se ejecuta.
    /// Si <paramref name="destructiva"/> es true, el botón de aceptar es rojo y dice «Ejecutar de todos modos».
    /// Devuelve true si el usuario confirma. Las contraseñas se muestran enmascaradas.
    /// </summary>
    public static bool ConfirmarSql(IWin32Window? duenio, string titulo, string sql, bool destructiva = false)
    {
        var aviso = destructiva
            ? "⚠  Esta acción es destructiva y no se puede deshacer. Revise la sentencia antes de continuar."
            : "Se ejecutará la siguiente sentencia en Oracle:";
        using var dlg = new DlgSql(DlgSql.Modo.Confirmar, titulo, Sentencias.OcultarClaves(sql), aviso, destructiva);
        return dlg.ShowDialog(duenio) == DialogResult.OK;
    }

    /// <summary>Solo muestra el SQL (botón «Ver SQL»), sin ejecutar nada.</summary>
    public static void VerSql(IWin32Window? duenio, string titulo, string sql)
    {
        using var dlg = new DlgSql(DlgSql.Modo.Ver, titulo, Sentencias.OcultarClaves(sql));
        dlg.ShowDialog(duenio);
    }

    /// <summary>
    /// Muestra un error. Para <c>OracleException</c> enseña el código ORA-xxxxx y el texto completos,
    /// nunca un «Error» genérico. Las validaciones (<see cref="ArgumentException"/>) se muestran como advertencia.
    /// </summary>
    public static void Error(IWin32Window? duenio, Exception ex)
    {
        switch (ex)
        {
            case ArgumentException or InvalidOperationException:
                using (var aviso = new DlgSql(DlgSql.Modo.Aviso, "Revise los datos", ex.Message))
                    aviso.ShowDialog(duenio);
                return;

            case OracleException ora:
                var codigo = $"ORA-{ora.Number:00000}";
                var detalle = new StringBuilder(ora.Message.Trim());
                if (!ora.Message.Contains(codigo))
                    detalle.Insert(0, codigo + ": ");
                if (Pistas.TryGetValue(ora.Number, out var pista))
                    detalle.Append("\r\n\r\n💡 ").Append(pista);
                using (var dlg = new DlgSql(DlgSql.Modo.Error, $"Oracle respondió {codigo}", detalle.ToString(),
                           "La operación no se completó. Este es el mensaje exacto de la base de datos:"))
                    dlg.ShowDialog(duenio);
                return;

            default:
                using (var dlg = new DlgSql(DlgSql.Modo.Error, "Ocurrió un error inesperado", $"{ex.GetType().Name}: {ex.Message}"))
                    dlg.ShowDialog(duenio);
                return;
        }
    }

    /// <summary>Aviso de éxito breve: aparece abajo a la derecha y se va solo (no bloquea).</summary>
    public static void Exito(IWin32Window? duenio, string mensaje) => Toast.Mostrar(duenio, mensaje, TipoEstado.Ok);

    /// <summary>
    /// Pregunta dónde guardar y exporta la tabla a CSV (UTF-8 con BOM y «;» para que Excel respete los acentos).
    /// Devuelve true si se guardó.
    /// </summary>
    public static bool ExportarCsv(IWin32Window? duenio, DataTable tabla, string nombreSugerido)
    {
        using var guardar = new SaveFileDialog
        {
            Title = "Exportar a CSV",
            Filter = "CSV (separado por punto y coma)|*.csv",
            FileName = $"{nombreSugerido}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            AddExtension = true
        };
        if (guardar.ShowDialog(duenio) != DialogResult.OK)
            return false;

        try
        {
            File.WriteAllText(guardar.FileName, Csv.Generar(tabla), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Exito(duenio, $"Se exportaron {tabla.Rows.Count} filas a:\n{guardar.FileName}");
            return true;
        }
        catch (IOException ex)
        {
            Error(duenio, ex);
            return false;
        }
    }
}

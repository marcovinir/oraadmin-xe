using System.Diagnostics;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Visor del .LOG de Data Pump (issue #12). Lo reutilizan Recuperación y Directorios y archivos (#18):
/// <c>DlgLog.Mostrar(this, ruta)</c>. Resalta en rojo las líneas con ORA- y en verde la de éxito.
/// </summary>
public sealed class DlgLog : VentanaBase
{
    private readonly RichTextBox _txtLog = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, WordWrap = false,
        Font = Tema.FuenteCodigo, BackColor = Tema.Fondo, ForeColor = Tema.Texto, DetectUrls = false
    };

    private DlgLog(string ruta, string contenido)
    {
        Text = $"Log de Data Pump · {Path.GetFileName(ruta)}";
        BackColor = Tema.Superficie;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(640, 360);
        Size = new Size(980, 620);

        var errores = Errores(contenido);
        var encabezado = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(20, 12, 20, 4) };
        var lblTitulo = new Label { Text = Path.GetFileName(ruta), Font = new Font("Segoe UI Semibold", 13f), AutoSize = true, Dock = DockStyle.Top };
        var lblRuta = new Label
        {
            Text = errores == 0 ? ruta : $"{ruta}   ·   {errores} {(errores == 1 ? "línea" : "líneas")} con ORA-",
            ForeColor = errores == 0 ? Tema.TextoSecundario : Tema.Peligro, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 6, 0, 0)
        };
        encabezado.Controls.Add(lblRuta);
        encabezado.Controls.Add(lblTitulo);

        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Tema.Borde };
        var interior = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 4, 4), BackColor = Tema.Fondo };
        interior.Controls.Add(_txtLog);
        marco.Controls.Add(interior);
        var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 8) };
        cuerpo.Controls.Add(marco);

        var btnCerrar = new BotonPlano { Text = "Cerrar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36), Margin = new Padding(8, 0, 0, 0) };
        var btnCopiar = new BotonPlano { Text = "Copiar", Icono = "", Estilo = EstiloBoton.Fantasma, MinimumSize = new Size(110, 36), Margin = new Padding(8, 0, 0, 0) };
        var btnCarpeta = new BotonPlano { Text = "Abrir carpeta", Icono = "", Estilo = EstiloBoton.Fantasma, MinimumSize = new Size(110, 36), Margin = new Padding(8, 0, 0, 0) };
        btnCopiar.Click += (_, _) => { Clipboard.SetText(_txtLog.Text); btnCopiar.Text = "Copiado"; };
        btnCarpeta.Click += (_, _) => Process.Start("explorer.exe", $"/select,\"{ruta}\"");
        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 10), BackColor = Tema.Fondo };
        pie.Controls.AddRange([btnCerrar, btnCopiar, btnCarpeta]);

        Controls.Add(cuerpo);
        Controls.Add(pie);
        Controls.Add(encabezado);
        AcceptButton = CancelButton = btnCerrar;

        Pintar(contenido);
        // Barras de desplazamiento oscuras, como las del resto de la app (Tema solo lo hace con TextBox y grillas).
        _txtLog.HandleCreated += (_, _) => _ = SetWindowTheme(_txtLog.Handle, "DarkMode_Explorer", null);
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    /// <summary>Abre el log; si el archivo ya no está en disco, lo avisa.</summary>
    public static void Mostrar(IWin32Window? duenio, string ruta)
    {
        string contenido;
        try
        {
            contenido = File.ReadAllText(ruta);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Dialogos.Error(duenio, new InvalidOperationException($"El archivo de log ya no está en disco:\r\n{ruta}"));
            return;
        }
        catch (IOException ex)
        {
            Dialogos.Error(duenio, ex);
            return;
        }
        using var dlg = new DlgLog(ruta, contenido);
        dlg.ShowDialog(duenio);
    }

    /// <summary>Líneas del log que traen un error de Oracle.</summary>
    public static int Errores(string contenido) =>
        contenido.Split('\n').Count(l => l.Contains("ORA-", StringComparison.Ordinal));

    private void Pintar(string contenido)
    {
        _txtLog.Text = contenido.Replace("\r\n", "\n");
        var inicio = 0;
        foreach (var linea in _txtLog.Text.Split('\n'))
        {
            Color? color = linea.Contains("ORA-", StringComparison.Ordinal) ? Tema.Peligro
                : linea.Contains("correctamente", StringComparison.OrdinalIgnoreCase) || linea.Contains("successfully", StringComparison.OrdinalIgnoreCase) ? Tema.Ok
                : linea.StartsWith(". .", StringComparison.Ordinal) ? Tema.AcentoTexto
                : null;
            if (color is { } c)
            {
                _txtLog.Select(inicio, linea.Length);
                _txtLog.SelectionColor = c;
            }
            inicio += linea.Length + 1;
        }
        _txtLog.Select(0, 0);
    }
}

using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Visor acotado de logs seleccionados desde un DIRECTORY ya verificado.</summary>
public sealed class DlgLogArchivoDirectorio : VentanaBase
{
    public DlgLogArchivoDirectorio(string nombre, LogLeido log)
    {
        Text = $"Log de Data Pump — {nombre}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 360);
        Size = new Size(980, 620);
        MinimizeBox = false;
        ShowInTaskbar = false;

        var aviso = new Label
        {
            Dock = DockStyle.Top, Height = log.Truncado ? 34 : 0,
            Text = "Se muestran solo los primeros 1 MiB del archivo.", ForeColor = Tema.Advertencia,
            Padding = new Padding(20, 8, 20, 0)
        };
        var texto = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, WordWrap = false,
            DetectUrls = false, Font = Tema.FuenteCodigo, BackColor = Tema.Fondo, ForeColor = Tema.Texto,
            Text = log.Contenido
        };
        var cerrar = new BotonPlano { Text = "Cerrar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 8) };
        pie.Controls.Add(cerrar);
        Controls.Add(texto);
        Controls.Add(aviso);
        Controls.Add(pie);
        AcceptButton = CancelButton = cerrar;
    }
}

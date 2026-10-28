using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Solicita solamente el basename de un DMP; la validación definitiva ocurre al confirmar.</summary>
public sealed class DlgRenombrarDump : VentanaBase
{
    private readonly TextBox _txtNombre = new() { Dock = DockStyle.Top, MaxLength = 185 };

    public string NombreNuevo => _txtNombre.Text.Trim();

    public DlgRenombrarDump(string nombreActual)
    {
        Text = "Renombrar archivo DMP";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 220);
        Size = new Size(620, 250);
        MinimizeBox = false;
        ShowInTaskbar = false;

        var etiqueta = new Label { Dock = DockStyle.Top, Height = 54, Text = "Nuevo nombre (.dmp). No se permiten rutas, separadores ni caracteres especiales.", Padding = new Padding(20, 14, 20, 0), ForeColor = Tema.TextoSecundario };
        _txtNombre.Text = nombreActual;
        _txtNombre.SelectAll();
        var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 8) };
        cuerpo.Controls.Add(_txtNombre);
        var cancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        var aceptar = new BotonPlano { Text = "Continuar", DialogResult = DialogResult.OK, Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 8) };
        pie.Controls.AddRange([aceptar, cancelar]);
        Controls.Add(cuerpo);
        Controls.Add(etiqueta);
        Controls.Add(pie);
        AcceptButton = aceptar;
        CancelButton = cancelar;
    }
}

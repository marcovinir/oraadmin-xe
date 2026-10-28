using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>Segundo factor local para DROP: el nombre debe escribirse completo.</summary>
public sealed class DlgEliminarTablespace : VentanaBase
{
    private readonly string _nombre;
    private readonly TextBox _txtConfirmacion = new() { Width = 330 };
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro };

    public DlgEliminarTablespace(string nombre)
    {
        _nombre = DdlTablespaces.ValidarNombre(nombre);
        Text = $"Eliminar {_nombre}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(570, 220);

        var contenido = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(24), WrapContents = false };
        contenido.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(510, 0), Text = $"Esta acción elimina el tablespace y sus datafiles registrados por Oracle. Escriba {_nombre} para continuar." });
        contenido.Controls.Add(_txtConfirmacion);
        contenido.Controls.Add(_lblError);
        var btnContinuar = Tema.Boton("Ver SQL", primario: true, icono: "\uE8A7");
        btnContinuar.DialogResult = DialogResult.OK;
        var btnCancelar = Tema.Boton("Cancelar");
        btnCancelar.DialogResult = DialogResult.Cancel;
        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 8, 16, 8) };
        pie.Controls.AddRange([btnContinuar, btnCancelar]);
        Controls.Add(contenido);
        Controls.Add(pie);
        AcceptButton = btnContinuar;
        CancelButton = btnCancelar;
        FormClosing += ValidarCierre;
    }

    private void ValidarCierre(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && !string.Equals(_txtConfirmacion.Text.Trim(), _nombre, StringComparison.Ordinal))
        {
            e.Cancel = true;
            _lblError.Text = "Debe escribir exactamente el nombre del tablespace.";
        }
    }
}

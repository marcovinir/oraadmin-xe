using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Diálogo acotado para otorgar READ y WRITE sobre un DIRECTORY.</summary>
public sealed class DlgOtorgarDirectorio : VentanaBase
{
    private readonly TextBox _txtUsuario = new() { Width = 320, CharacterCasing = CharacterCasing.Upper };
    private readonly Label _lblError = new() { AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = Tema.Peligro };

    public string Usuario { get; private set; } = "";

    public DlgOtorgarDirectorio(string directorio)
    {
        Text = $"Otorgar READ/WRITE · {directorio}";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var aviso = new Label
        {
            Text = "Se validará que el usuario exista y que no sea una cuenta interna de Oracle.",
            AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = Tema.TextoSecundario
        };
        var cuerpo = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(20, 16, 20, 4) };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cuerpo.Controls.Add(new Label { Text = "Usuario", AutoSize = true, Margin = new Padding(0, 7, 8, 12) }, 0, 0);
        cuerpo.Controls.Add(_txtUsuario, 1, 0);
        cuerpo.Controls.Add(new Label(), 0, 1);
        cuerpo.Controls.Add(aviso, 1, 1);
        cuerpo.Controls.Add(new Label(), 0, 2);
        cuerpo.Controls.Add(_lblError, 1, 2);

        var aceptar = new BotonPlano { Text = "Continuar", Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        aceptar.Click += (_, _) => Aceptar();
        var cancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        var pie = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, Size = new Size(500, 64), Padding = new Padding(16, 8, 16, 16)
        };
        pie.Controls.AddRange([cancelar, aceptar]);

        var raiz = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        raiz.Controls.AddRange([cuerpo, pie]);
        Controls.Add(raiz);
        AcceptButton = aceptar;
        CancelButton = cancelar;
        _txtUsuario.Focus();
    }

    private void Aceptar()
    {
        try
        {
            Usuario = Identificador.Validar(_txtUsuario.Text, "usuario");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
    }
}

using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Recoge un nombre DIRECTORY y una ruta local; no crea carpetas ni ejecuta SQL.</summary>
public sealed class DlgDirectorio : VentanaBase
{
    private readonly TextBox _txtNombre = new() { Width = 390, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox _txtRuta = new() { Width = 390 };
    private readonly Label _lblRuta = new() { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = Tema.TextoSecundario };
    private readonly CheckBox _chkCrearCarpeta = new()
    {
        Text = "Confirmo crear la carpeta local si todavía no existe.", AutoSize = true, Visible = false
    };
    private readonly Label _lblError = new() { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = Tema.Peligro };

    public string Nombre { get; private set; } = "";
    public string Ruta { get; private set; } = "";
    public bool CrearCarpeta { get; private set; }

    public DlgDirectorio()
    {
        Text = "Crear objeto DIRECTORY";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var btnExaminar = Tema.Boton("Examinar…", icono: "");
        btnExaminar.Click += (_, _) => ElegirCarpeta();
        _txtRuta.TextChanged += (_, _) => ActualizarRuta();

        var ruta = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        ruta.Controls.AddRange([_txtRuta, btnExaminar]);
        var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(20, 16, 20, 4) };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.Controls.Add(new Label { Text = "Nombre", AutoSize = true, Margin = new Padding(0, 7, 8, 12) }, 0, 0);
        tabla.Controls.Add(_txtNombre, 1, 0);
        tabla.Controls.Add(new Label { Text = "Carpeta", AutoSize = true, Margin = new Padding(0, 7, 8, 12) }, 0, 1);
        tabla.Controls.Add(ruta, 1, 1);
        tabla.Controls.Add(new Label(), 0, 2);
        tabla.Controls.Add(_lblRuta, 1, 2);
        tabla.Controls.Add(new Label(), 0, 3);
        tabla.Controls.Add(_chkCrearCarpeta, 1, 3);
        tabla.Controls.Add(new Label(), 0, 4);
        tabla.Controls.Add(_lblError, 1, 4);

        var btnCrear = new BotonPlano { Text = "Continuar", Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        btnCrear.Click += (_, _) => Aceptar();
        var btnCancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        var pie = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, Size = new Size(570, 64), Padding = new Padding(16, 8, 16, 16)
        };
        pie.Controls.AddRange([btnCancelar, btnCrear]);

        var raiz = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        raiz.Controls.AddRange([tabla, pie]);
        Controls.Add(raiz);
        AcceptButton = btnCrear;
        CancelButton = btnCancelar;
        ActualizarRuta();
        _txtNombre.Focus();
    }

    private void ElegirCarpeta()
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Elija una carpeta local para el objeto DIRECTORY.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (Directory.Exists(_txtRuta.Text)) dialogo.InitialDirectory = _txtRuta.Text;
        if (dialogo.ShowDialog(this) == DialogResult.OK) _txtRuta.Text = dialogo.SelectedPath;
    }

    private void ActualizarRuta()
    {
        var ruta = _txtRuta.Text.Trim();
        var existe = ruta.Length > 0 && Directory.Exists(ruta);
        _lblRuta.Text = ruta.Length == 0 ? $"Elija una ruta bajo {DdlDirectorios.RutaRaizRespaldos}." :
            existe ? "La carpeta existe localmente. Esto no comprueba permisos del servicio Oracle." :
                     "La carpeta no existe localmente. Se requerirá su confirmación explícita para crearla.";
        _lblRuta.ForeColor = existe ? Tema.Ok : ruta.Length == 0 ? Tema.TextoSecundario : Tema.Advertencia;
        _chkCrearCarpeta.Visible = ruta.Length > 0 && !existe;
        if (!_chkCrearCarpeta.Visible) _chkCrearCarpeta.Checked = false;
    }

    private void Aceptar()
    {
        try
        {
            Nombre = OraAdminXE.Datos.Identificador.Validar(_txtNombre.Text, "directorio");
            Ruta = DdlDirectorios.ValidarRutaCreacion(_txtRuta.Text);
            CrearCarpeta = !Directory.Exists(Ruta);
            if (CrearCarpeta && !_chkCrearCarpeta.Checked)
                throw new ArgumentException("Marque la confirmación para crear la carpeta local faltante.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
    }
}

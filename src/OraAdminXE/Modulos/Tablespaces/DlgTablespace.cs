using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>Datos necesarios para crear un tablespace permanente de demostración.</summary>
public sealed class DlgTablespace : VentanaBase
{
    private readonly TextBox _txtNombre = new() { Width = 330, Text = "TS_VENTAS" };
    private readonly TextBox _txtRuta;
    private readonly NumericUpDown _numInicial = Numero(50);
    private readonly CheckBox _chkAutoextend = new() { Text = "Habilitar autoextend", Checked = true, AutoSize = true };
    private readonly NumericUpDown _numNext = Numero(10);
    private readonly NumericUpDown _numMax = Numero(500);
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro, MaximumSize = new Size(520, 0) };
    private readonly long _maximoMb;

    public string Nombre => _txtNombre.Text;
    public string Ruta => _txtRuta.Text;
    public long InicialMb => decimal.ToInt64(_numInicial.Value);
    public bool Autoextend => _chkAutoextend.Checked;
    public long NextMb => decimal.ToInt64(_numNext.Value);
    public long MaxMb => decimal.ToInt64(_numMax.Value);

    public DlgTablespace(string rutaSugerida, string directorioPermitido, long maximoMb)
    {
        _maximoMb = maximoMb;
        Text = "Crear tablespace";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(600, 355);
        _txtRuta = new TextBox { Width = 430, Text = rutaSugerida, Tag = directorioPermitido };
        _numInicial.Maximum = _numNext.Maximum = _numMax.Maximum = maximoMb;

        var contenido = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, AutoScroll = true };
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Fila(contenido, 0, "Nombre", _txtNombre);
        Fila(contenido, 1, "Datafile", _txtRuta);
        Fila(contenido, 2, "Tamaño inicial (MB)", _numInicial);
        Fila(contenido, 3, "", _chkAutoextend);
        Fila(contenido, 4, "NEXT (MB)", _numNext);
        Fila(contenido, 5, "MAXSIZE (MB)", _numMax);
        contenido.Controls.Add(_lblError, 1, 6);

        var btnCrear = Tema.Boton("Crear", primario: true, icono: "\uE710");
        btnCrear.DialogResult = DialogResult.OK;
        var btnCancelar = Tema.Boton("Cancelar");
        btnCancelar.DialogResult = DialogResult.Cancel;
        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 8, 16, 8) };
        pie.Controls.AddRange([btnCrear, btnCancelar]);
        Controls.Add(contenido);
        Controls.Add(pie);
        AcceptButton = btnCrear;
        CancelButton = btnCancelar;
        _chkAutoextend.CheckedChanged += (_, _) => { _numNext.Enabled = _numMax.Enabled = _chkAutoextend.Checked; };
        FormClosing += (_, e) => ValidarCierre(e, directorioPermitido);
    }

    private void ValidarCierre(FormClosingEventArgs e, string directorio)
    {
        if (DialogResult != DialogResult.OK) return;
        try
        {
            DdlTablespaces.ValidarNombrePrueba(Nombre);
            DdlTablespaces.ValidarTamano(InicialMb, "tamaño inicial", _maximoMb);
            DdlTablespaces.ValidarRutaDatafile(Ruta, directorio, verificarSistemaArchivos: false);
            if (Autoextend) DdlTablespaces.ValidarAutoextend(InicialMb, NextMb, MaxMb, _maximoMb);
        }
        catch (ArgumentException ex)
        {
            e.Cancel = true;
            _lblError.Text = ex.Message;
        }
    }

    private static NumericUpDown Numero(decimal valor) => new() { Width = 150, Minimum = 1, Maximum = DdlTablespaces.MaximoMbUi, Value = valor, ThousandsSeparator = true };

    private static void Fila(TableLayoutPanel panel, int fila, string etiqueta, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) }, 0, fila);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 5, 0, 5);
        panel.Controls.Add(control, 1, fila);
    }
}

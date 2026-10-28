using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

public enum ModoDatafile { Agregar, Redimensionar, Autoextend }

/// <summary>Diálogo acotado para una operación sobre un datafile previamente revalidado.</summary>
public sealed class DlgDatafile : VentanaBase
{
    private readonly ModoDatafile _modo;
    private readonly TextBox _txtRuta;
    private readonly NumericUpDown _numTamano;
    private readonly CheckBox _chkAutoextend = new() { Text = "Habilitar autoextend", AutoSize = true };
    private readonly NumericUpDown _numNext;
    private readonly NumericUpDown _numMax;
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro, MaximumSize = new Size(520, 0) };
    private readonly long _maximoMb;

    public string Ruta => _txtRuta.Text;
    public long TamanoMb => decimal.ToInt64(_numTamano.Value);
    public bool Autoextend => _chkAutoextend.Checked;
    public long NextMb => decimal.ToInt64(_numNext.Value);
    public long MaxMb => decimal.ToInt64(_numMax.Value);

    public DlgDatafile(ModoDatafile modo, string ruta, long tamanoActualMb, long maxActualMb = 0, long nextActualMb = 10,
        long maximoMb = DdlTablespaces.MaximoMbUi)
    {
        _modo = modo;
        _maximoMb = maximoMb;
        Text = modo switch
        {
            ModoDatafile.Agregar => "Agregar datafile",
            ModoDatafile.Redimensionar => "Redimensionar datafile",
            _ => "Configurar autoextend"
        };
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(600, modo == ModoDatafile.Autoextend ? 285 : 220);
        _txtRuta = new TextBox { Width = 430, Text = ruta, ReadOnly = modo != ModoDatafile.Agregar };
        _numTamano = Numero(modo == ModoDatafile.Redimensionar ? Math.Max(tamanoActualMb, 100) : tamanoActualMb);
        _numNext = Numero(Math.Max(1, nextActualMb));
        _numMax = Numero(Math.Max(tamanoActualMb + Math.Max(1, nextActualMb), maxActualMb));
        _numTamano.Maximum = _numNext.Maximum = _numMax.Maximum = maximoMb;
        _chkAutoextend.Checked = modo == ModoDatafile.Autoextend;

        var contenido = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, AutoScroll = true };
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Fila(contenido, 0, "Datafile", _txtRuta);
        if (modo is ModoDatafile.Agregar or ModoDatafile.Redimensionar)
            Fila(contenido, 1, modo == ModoDatafile.Agregar ? "Tamaño (MB)" : "Nuevo tamaño (MB)", _numTamano);
        if (modo == ModoDatafile.Autoextend)
        {
            Fila(contenido, 1, "", _chkAutoextend);
            Fila(contenido, 2, "NEXT (MB)", _numNext);
            Fila(contenido, 3, "MAXSIZE (MB)", _numMax);
            _chkAutoextend.CheckedChanged += (_, _) => _numNext.Enabled = _numMax.Enabled = _chkAutoextend.Checked;
        }
        contenido.Controls.Add(_lblError, 1, 5);

        var btnAceptar = Tema.Boton("Continuar", primario: true, icono: "\uE73E");
        btnAceptar.DialogResult = DialogResult.OK;
        var btnCancelar = Tema.Boton("Cancelar");
        btnCancelar.DialogResult = DialogResult.Cancel;
        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 8, 16, 8) };
        pie.Controls.AddRange([btnAceptar, btnCancelar]);
        Controls.Add(contenido);
        Controls.Add(pie);
        AcceptButton = btnAceptar;
        CancelButton = btnCancelar;
        FormClosing += (_, e) => ValidarCierre(e, tamanoActualMb);
    }

    private void ValidarCierre(FormClosingEventArgs e, long tamanoActualMb)
    {
        if (DialogResult != DialogResult.OK) return;
        try
        {
            if (_modo is ModoDatafile.Agregar or ModoDatafile.Redimensionar)
                DdlTablespaces.ValidarTamano(TamanoMb, "tamaño", _maximoMb);
            if (_modo == ModoDatafile.Autoextend && Autoextend)
                DdlTablespaces.ValidarAutoextend(tamanoActualMb, NextMb, MaxMb, _maximoMb);
        }
        catch (ArgumentException ex)
        {
            e.Cancel = true;
            _lblError.Text = ex.Message;
        }
    }

    private static NumericUpDown Numero(long valor) => new() { Width = 150, Minimum = 1, Maximum = DdlTablespaces.MaximoMbUi, Value = Math.Clamp(valor, 1, DdlTablespaces.MaximoMbUi), ThousandsSeparator = true };

    private static void Fila(TableLayoutPanel panel, int fila, string etiqueta, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) }, 0, fila);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 5, 0, 5);
        panel.Controls.Add(control, 1, fila);
    }
}

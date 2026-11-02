using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Diálogo para definir una política permitida por la pantalla de Auditoría.</summary>
public sealed class DlgCrearPoliticaAuditoria : VentanaBase
{
    private readonly RadioButton _rdoObjeto = new() { Text = "Por objeto", AutoSize = true, Checked = true };
    private readonly RadioButton _rdoSistema = new() { Text = "De sistema", AutoSize = true };
    private readonly TextBox _txtPolitica = new() { PlaceholderText = "POL_DEMO_DML", Dock = DockStyle.Fill, MinimumSize = new Size(330, 0) };
    private readonly TextBox _txtEsquema = new() { PlaceholderText = "DEMO_VENTAS", Dock = DockStyle.Fill, MinimumSize = new Size(330, 0) };
    private readonly TextBox _txtTabla = new() { PlaceholderText = "FACTURA", Dock = DockStyle.Fill, MinimumSize = new Size(330, 0) };
    private readonly CheckedListBox _lstObjeto = new() { CheckOnClick = true, Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly CheckedListBox _lstSistema = new() { CheckOnClick = true, Dock = DockStyle.Fill, IntegralHeight = false, Visible = false };
    private readonly Label _lblEsquema = Etiqueta("Esquema");
    private readonly Label _lblTabla = Etiqueta("Tabla");
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro, MaximumSize = new Size(500, 0), Dock = DockStyle.Fill };
    private readonly Panel _pnlAcciones = new() { Dock = DockStyle.Fill, Height = AltoAccionesObjeto };
    private const int AltoAccionesObjeto = 104;
    private const int AltoAccionesSistema = 174;

    public DlgCrearPoliticaAuditoria()
    {
        Text = "Crear política de auditoría";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(580, 480);
        Size = new Size(660, 540);

        _lstObjeto.Items.AddRange(DdlPoliticasAuditoria.AccionesObjeto.Cast<object>().ToArray());
        _lstSistema.Items.AddRange(DdlPoliticasAuditoria.AccionesSistema.Cast<object>().ToArray());
        _rdoObjeto.CheckedChanged += (_, _) => CambiarTipo();
        _rdoSistema.CheckedChanged += (_, _) => CambiarTipo();

        var tipo = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Dock = DockStyle.Fill };
        tipo.Controls.AddRange([_rdoObjeto, _rdoSistema]);
        _pnlAcciones.Controls.Add(_lstObjeto);
        _pnlAcciones.Controls.Add(_lstSistema);

        var tabla = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Padding = new Padding(24, 20, 24, 8)
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Fila(tabla, "Nombre", _txtPolitica);
        Fila(tabla, "Tipo", tipo);
        Fila(tabla, _lblEsquema, _txtEsquema);
        Fila(tabla, _lblTabla, _txtTabla);
        Fila(tabla, "Acciones", _pnlAcciones);
        Fila(tabla, new Label(), _lblError);

        var btnCrear = new BotonPlano { Text = "Crear", Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        var btnCancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        btnCrear.Click += (_, _) => Aceptar();
        var pie = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, Padding = new Padding(0), WrapContents = false };
        pie.Controls.AddRange([btnCancelar, btnCrear]);
        var pieContenedor = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(24, 10, 24, 12) };
        pieContenedor.Controls.Add(pie);
        var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        contenido.Controls.Add(tabla);
        Controls.Add(contenido);
        Controls.Add(pieContenedor);
        AcceptButton = btnCrear;
        CancelButton = btnCancelar;
        CambiarTipo();
    }

    public bool EsObjeto => _rdoObjeto.Checked;
    public string Politica => _txtPolitica.Text;
    public string Esquema => _txtEsquema.Text;
    public string Tabla => _txtTabla.Text;
    public IReadOnlyList<string> Acciones => (EsObjeto ? _lstObjeto : _lstSistema).CheckedItems.Cast<string>().ToArray();

    private static Label Etiqueta(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 8, 18, 8)
    };

    private static void Fila(TableLayoutPanel tabla, string etiqueta, Control control) => Fila(tabla, Etiqueta(etiqueta), control);

    private static void Fila(TableLayoutPanel tabla, Label etiqueta, Control control)
    {
        var fila = tabla.RowCount++;
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 4, 0, 4);
        tabla.Controls.Add(etiqueta, 0, fila);
        tabla.Controls.Add(control, 1, fila);
    }

    private void CambiarTipo()
    {
        var objeto = _rdoObjeto.Checked;
        _lblEsquema.Visible = _txtEsquema.Visible = objeto;
        _lblTabla.Visible = _txtTabla.Visible = objeto;
        _lstObjeto.Visible = objeto;
        _lstSistema.Visible = !objeto;
        _pnlAcciones.Height = objeto ? AltoAccionesObjeto : AltoAccionesSistema;
        _pnlAcciones.MinimumSize = new Size(0, _pnlAcciones.Height);
        PerformLayout();
    }

    private void Aceptar()
    {
        try
        {
            if (EsObjeto)
                _ = DdlPoliticasAuditoria.CrearObjeto(Politica, Esquema, Tabla, Acciones);
            else
                _ = DdlPoliticasAuditoria.CrearSistema(Politica, Acciones);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
    }
}

internal sealed class DlgActivarPoliticaAuditoria : VentanaBase
{
    private readonly TextBox _txtUsuario = new() { PlaceholderText = "Vacío = todos los usuarios", Dock = DockStyle.Fill, MinimumSize = new Size(300, 0) };
    private readonly ComboBox _cboResultado = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, MinimumSize = new Size(300, 0) };

    public DlgActivarPoliticaAuditoria(string politica)
    {
        Text = $"Activar {politica}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(550, 238);
        _cboResultado.Items.AddRange(["Éxitos y fallos", "Solo éxitos", "Solo fallos"]);
        _cboResultado.SelectedIndex = 0;
        var tabla = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Dock = DockStyle.Top, Padding = new Padding(24, 20, 24, 8) };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Fila(tabla, "Usuario (opcional)", _txtUsuario);
        Fila(tabla, "Resultado", _cboResultado);
        var aceptar = new BotonPlano { Text = "Continuar", Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        var cancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        aceptar.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        var pie = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, WrapContents = false };
        pie.Controls.AddRange([cancelar, aceptar]);
        var pieContenedor = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(24, 10, 24, 12) };
        pieContenedor.Controls.Add(pie);
        var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        contenido.Controls.Add(tabla);
        Controls.Add(contenido);
        Controls.Add(pieContenedor);
        AcceptButton = aceptar;
        CancelButton = cancelar;
    }

    public string? Usuario => string.IsNullOrWhiteSpace(_txtUsuario.Text) ? null : _txtUsuario.Text;
    public FiltroResultadoAuditoria Filtro => (FiltroResultadoAuditoria)_cboResultado.SelectedIndex;

    private static void Fila(TableLayoutPanel tabla, string texto, Control control)
    {
        var fila = tabla.RowCount++;
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.Controls.Add(new Label { Text = texto, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 18, 8) }, 0, fila);
        control.Margin = new Padding(0, 4, 0, 4);
        tabla.Controls.Add(control, 1, fila);
    }
}

internal sealed class DlgConfirmarEliminacionPolitica : VentanaBase
{
    private readonly TextBox _txtNombre = new() { Dock = DockStyle.Fill, MinimumSize = new Size(390, 0) };

    public DlgConfirmarEliminacionPolitica(string politica)
    {
        Text = "Confirmar eliminación";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(590, 240);
        var aviso = new Label
        {
            Text = $"Escriba exactamente {politica} para habilitar la eliminación.",
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = Tema.Peligro,
            Margin = new Padding(0, 0, 0, 12)
        };
        var aceptar = new BotonPlano { Text = "Continuar", Estilo = EstiloBoton.Peligro, MinimumSize = new Size(110, 36) };
        var cancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, MinimumSize = new Size(110, 36) };
        aceptar.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        var contenido = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 22, 24, 8) };
        var campos = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        campos.Controls.AddRange([aviso, _txtNombre]);
        contenido.Controls.Add(campos);
        var pie = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, WrapContents = false };
        pie.Controls.AddRange([cancelar, aceptar]);
        var pieContenedor = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(24, 10, 24, 12) };
        pieContenedor.Controls.Add(pie);
        Controls.Add(contenido);
        Controls.Add(pieContenedor);
        AcceptButton = aceptar;
        CancelButton = cancelar;
    }

    public bool Confirma(string politica) => string.Equals(_txtNombre.Text.Trim(), politica, StringComparison.Ordinal);
}

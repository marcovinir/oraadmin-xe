using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Crear un respaldo programado (issue #30): nombre del job, qué respaldar (como en el asistente), directorio y frecuencia.
/// Muestra el repeat_interval que se le pasará a DBMS_SCHEDULER.
/// </summary>
public sealed class DlgProgramacion : VentanaBase
{
    private readonly TextBox _txtNombre = new() { CharacterCasing = CharacterCasing.Upper, Width = 330, MaxLength = 100 };
    private readonly RadioButton _rdoEsquema = new() { Text = "Por esquema", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 16, 0) };
    private readonly RadioButton _rdoTabla = new() { Text = "Por tabla", AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
    private readonly RadioButton _rdoFull = new() { Text = "Full", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
    private readonly CheckedListBox _clbEsquemas = new() { CheckOnClick = true, IntegralHeight = false, Size = new Size(330, 110) };
    private readonly ComboBox _cboEsquema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly CheckedListBox _clbTablas = new() { CheckOnClick = true, IntegralHeight = false, Size = new Size(330, 110) };
    private readonly Label _lblFull = new() { Text = "Toda la PDB (tarda un par de minutos).", AutoSize = true, ForeColor = Tema.Advertencia, Margin = new Padding(0, 6, 0, 0) };
    private readonly ComboBox _cboDirectorio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly ComboBox _cboFrecuencia = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _numHoras = new() { Minimum = 1, Maximum = 23, Value = 1, Width = 60 };
    private readonly ComboBox _cboDia = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly DateTimePicker _dtpHora = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 80 };
    private readonly Label _lblIntervalo = new() { AutoSize = true, ForeColor = Tema.TextoSecundario, Font = Tema.FuenteCodigo, Margin = new Padding(0, 8, 0, 0) };
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro };
    private readonly Label _lblHoras = Texto("horas");
    private readonly Label _lblALas = Texto("a las");
    private readonly Func<string, Task<string[]>> _tablasDe;

    public SolicitudRespaldo? Solicitud { get; private set; }
    public Frecuencia? Frecuencia { get; private set; }

    public DlgProgramacion(IEnumerable<string> esquemas, IEnumerable<DirectorioRespaldo> directorios, Func<string, Task<string[]>> tablasDe)
    {
        _tablasDe = tablasDe;
        Text = "Programar respaldo";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var lista = esquemas.ToArray();
        foreach (var e in lista) _clbEsquemas.Items.Add(e, e == "DEMO_VENTAS");
        _cboEsquema.Items.AddRange(lista.ToArray<object>());
        _cboEsquema.SelectedItem = lista.Contains("DEMO_VENTAS") ? "DEMO_VENTAS" : lista.FirstOrDefault();
        _cboDirectorio.Items.AddRange(directorios.ToArray<object>());
        if (_cboDirectorio.Items.Count > 0) _cboDirectorio.SelectedIndex = 0;
        _cboFrecuencia.Items.AddRange(["Cada N horas", "Diaria", "Semanal"]);
        _cboFrecuencia.SelectedIndex = 1;
        _cboDia.Items.AddRange(["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"]);
        _cboDia.SelectedIndex = 0;
        _dtpHora.Value = DateTime.Today.AddHours(22);
        _txtNombre.Text = "PRG_DEMO_VENTAS_DIARIO";

        var tipo = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        tipo.Controls.AddRange([_rdoEsquema, _rdoTabla, _rdoFull]);
        var objetos = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
        objetos.Controls.AddRange([_clbEsquemas, _cboEsquema, _clbTablas, _lblFull]);
        _cboEsquema.Margin = new Padding(0, 0, 0, 6);
        var frecuencia = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        frecuencia.Controls.AddRange([_cboFrecuencia, _numHoras, _lblHoras, _cboDia, _lblALas, _dtpHora]);
        foreach (Control c in frecuencia.Controls) c.Margin = new Padding(0, 0, 8, 0);

        var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(20, 18, 20, 4) };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        void Fila(string etiqueta, Control control) =>
            tabla.Controls.AddRange([new Label { Text = etiqueta, AutoSize = true, Margin = new Padding(0, 6, 16, 10) }, control]);
        Fila("Nombre del job", _txtNombre);
        Fila("Tipo", tipo);
        Fila("Objetos", objetos);
        Fila("Directorio", _cboDirectorio);
        Fila("Frecuencia", frecuencia);
        tabla.Controls.AddRange([new Label(), _lblIntervalo, new Label(), _lblError]);
        _txtNombre.Margin = _cboDirectorio.Margin = new Padding(0, 2, 0, 10);

        var btnCrear = new BotonPlano { Text = "Programar", Estilo = EstiloBoton.Primario, Icono = "", MinimumSize = new Size(120, 36) };
        var btnCancelar = new BotonPlano { Text = "Cancelar", MinimumSize = new Size(110, 36), DialogResult = DialogResult.Cancel };
        btnCrear.Click += (_, _) => Aceptar();
        var pie = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(16, 8, 16, 16) };
        pie.Controls.AddRange([btnCrear, btnCancelar]);

        var raiz = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        raiz.Controls.AddRange([tabla, pie]);
        pie.Width = tabla.PreferredSize.Width;
        Controls.Add(raiz);
        AcceptButton = btnCrear;
        CancelButton = btnCancelar;

        foreach (var rdo in new[] { _rdoEsquema, _rdoTabla, _rdoFull })
            rdo.CheckedChanged += (_, _) => { if (rdo.Checked) CambiarTipo(); };
        _cboEsquema.SelectedIndexChanged += async (_, _) => await CargarTablasAsync();
        _cboFrecuencia.SelectedIndexChanged += (_, _) => CambiarFrecuencia();
        _numHoras.ValueChanged += (_, _) => MostrarIntervalo();
        _cboDia.SelectedIndexChanged += (_, _) => MostrarIntervalo();
        _dtpHora.ValueChanged += (_, _) => MostrarIntervalo();
        Shown += async (_, _) => await CargarTablasAsync();
        CambiarTipo();
        CambiarFrecuencia();
    }

    private static Label Texto(string texto) => new() { Text = texto, AutoSize = true, ForeColor = Tema.TextoSecundario, Padding = new Padding(0, 6, 0, 0) };

    private TipoRespaldo Tipo => _rdoFull.Checked ? TipoRespaldo.Full : _rdoTabla.Checked ? TipoRespaldo.Table : TipoRespaldo.Schema;

    private void CambiarTipo()
    {
        _clbEsquemas.Visible = Tipo == TipoRespaldo.Schema;
        _cboEsquema.Visible = _clbTablas.Visible = Tipo == TipoRespaldo.Table;
        _lblFull.Visible = Tipo == TipoRespaldo.Full;
    }

    private async Task CargarTablasAsync()
    {
        _clbTablas.Items.Clear();
        if (_cboEsquema.SelectedItem is not string esquema) return;
        try { foreach (var t in await _tablasDe(esquema)) _clbTablas.Items.Add(t); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private Frecuencia FrecuenciaElegida() => new(
        (TipoFrecuencia)_cboFrecuencia.SelectedIndex, (int)_numHoras.Value, _dtpHora.Value.Hour, _dtpHora.Value.Minute, (DayOfWeek)_cboDia.SelectedIndex);

    private void CambiarFrecuencia()
    {
        var tipo = (TipoFrecuencia)_cboFrecuencia.SelectedIndex;
        var horas = tipo == TipoFrecuencia.CadaHoras;
        _numHoras.Visible = _lblHoras.Visible = horas;
        _cboDia.Visible = tipo == TipoFrecuencia.Semanal;
        _dtpHora.Visible = _lblALas.Visible = !horas;
        MostrarIntervalo();
    }

    private void MostrarIntervalo() => _lblIntervalo.Text = "repeat_interval: " + FrecuenciaElegida().RepeatInterval;

    private void Aceptar()
    {
        try
        {
            if (_cboDirectorio.SelectedItem is not DirectorioRespaldo dir) throw new ArgumentException("Elija el directorio.");
            var esquemas = Tipo switch
            {
                TipoRespaldo.Schema => _clbEsquemas.CheckedItems.Cast<string>().ToList(),
                TipoRespaldo.Table when _cboEsquema.SelectedItem is string e => [e],
                _ => new List<string>()
            };
            var tablas = Tipo == TipoRespaldo.Table ? _clbTablas.CheckedItems.Cast<string>().ToList() : [];
            var s = new SolicitudRespaldo(Tipo, dir, _txtNombre.Text.Trim(), esquemas, tablas);
            ServicioProgramaciones.BloqueJob(s);   // valida nombre, objetos y tamaño del job
            Solicitud = s;
            Frecuencia = FrecuenciaElegida();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
    }
}

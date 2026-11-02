using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Diálogo para crear un índice: esquema → tabla → columnas (marcar y ordenar con ▲▼), nombre sugerido
/// IX_{TABLA}_{COL} y casilla UNIQUE. No ejecuta nada: deja el DDL en <see cref="Sql"/>.
/// </summary>
public sealed class DlgIndice : VentanaBase
{
    private readonly Func<string, Task<string[]>> _tablasDe;
    private readonly Func<string, string, Task<string[]>> _columnasDe;
    private readonly ComboBox _cboEsquema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(20, 42), Width = 230 };
    private readonly ComboBox _cboTabla = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(270, 42), Width = 230 };
    private readonly CheckedListBox _lstColumnas = new() { Location = new Point(20, 104), Size = new Size(400, 170), CheckOnClick = true, IntegralHeight = false };
    private readonly BotonPlano _btnSubir = new() { Text = "▲", AutoSize = false, Location = new Point(432, 104), Size = new Size(68, 36) };
    private readonly BotonPlano _btnBajar = new() { Text = "▼", AutoSize = false, Location = new Point(432, 148), Size = new Size(68, 36) };
    private readonly TextBox _txtNombre = new() { Location = new Point(20, 312), Width = 480, MaxLength = 128 };
    private readonly CheckBox _chkUnico = new() { Text = "UNIQUE (no permite valores repetidos)", AutoSize = true, Location = new Point(20, 350) };
    private readonly Label _lblError = new() { Location = new Point(20, 382), Size = new Size(480, 44), ForeColor = Tema.Peligro };

    private int _versionTablas;
    private int _versionColumnas;
    private bool _nombreManual;
    private bool _sugiriendo;

    /// <summary>DDL del índice, listo para confirmar (solo válido si el diálogo cerró con OK).</summary>
    public string Sql { get; private set; } = "";

    public string Esquema => _cboEsquema.SelectedItem as string ?? "";

    public DlgIndice(IEnumerable<string> esquemas, string? esquemaInicial,
                     Func<string, Task<string[]>> tablasDe, Func<string, string, Task<string[]>> columnasDe)
    {
        _tablasDe = tablasDe;
        _columnasDe = columnasDe;
        Text = "Crear índice";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        ClientSize = new Size(520, 490);

        _cboEsquema.Items.AddRange(esquemas.ToArray<object>());
        var btnCrear = new BotonPlano { Text = "Crear índice", Estilo = EstiloBoton.Primario, AutoSize = false, Location = new Point(370, 436), Size = new Size(130, 36) };
        var btnCancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = false, Location = new Point(250, 436), Size = new Size(110, 36) };
        Controls.AddRange([
            new Label { Text = "Esquema", AutoSize = true, Location = new Point(20, 18) }, _cboEsquema,
            new Label { Text = "Tabla", AutoSize = true, Location = new Point(270, 18) }, _cboTabla,
            new Label { Text = "Columnas: marque las del índice y ordénelas con ▲ ▼", AutoSize = true, Location = new Point(20, 80) },
            _lstColumnas, _btnSubir, _btnBajar,
            new Label { Text = "Nombre del índice", AutoSize = true, Location = new Point(20, 288) }, _txtNombre,
            _chkUnico, _lblError, btnCrear, btnCancelar]);
        AcceptButton = btnCrear;
        CancelButton = btnCancelar;

        _cboEsquema.SelectedIndexChanged += async (_, _) => await CargarTablasAsync();
        _cboTabla.SelectedIndexChanged += async (_, _) => await CargarColumnasAsync();
        // ItemCheck ocurre antes de que cambie la marca: se recalcula el nombre cuando ya cambió.
        _lstColumnas.ItemCheck += (_, _) => BeginInvoke(ActualizarNombre);
        _btnSubir.Click += (_, _) => Mover(-1);
        _btnBajar.Click += (_, _) => Mover(+1);
        _txtNombre.TextChanged += (_, _) => { if (!_sugiriendo) _nombreManual = true; };
        btnCrear.Click += (_, _) => Aceptar();

        if (_cboEsquema.Items.Count > 0)
            _cboEsquema.SelectedItem = esquemaInicial is not null && _cboEsquema.Items.Contains(esquemaInicial) ? esquemaInicial : _cboEsquema.Items[0];
    }

    private async Task CargarTablasAsync()
    {
        var version = ++_versionTablas;
        _versionColumnas++;   // descarta una carga de columnas en curso: era de la tabla del esquema anterior
        try
        {
            _lblError.Text = "";
            _cboTabla.Items.Clear();
            _lstColumnas.Items.Clear();
            if (_cboEsquema.SelectedItem is not string esquema) return;
            var tablas = await _tablasDe(esquema);
            if (version != _versionTablas || IsDisposed) return;
            _cboTabla.Items.AddRange(tablas.ToArray<object>());
            if (_cboTabla.Items.Count > 0) _cboTabla.SelectedIndex = 0;
        }
        catch (Exception ex) { if (version == _versionTablas && !IsDisposed) _lblError.Text = ex.Message; }
    }

    private async Task CargarColumnasAsync()
    {
        var version = ++_versionColumnas;
        try
        {
            _lstColumnas.Items.Clear();
            _nombreManual = false;
            ActualizarNombre();
            if (_cboEsquema.SelectedItem is not string esquema || _cboTabla.SelectedItem is not string tabla) return;
            var columnas = await _columnasDe(esquema, tabla);
            if (version != _versionColumnas || IsDisposed) return;
            _lstColumnas.Items.AddRange(columnas.ToArray<object>());
        }
        catch (Exception ex) { if (version == _versionColumnas && !IsDisposed) _lblError.Text = ex.Message; }
    }

    private IEnumerable<string> Marcadas => _lstColumnas.CheckedItems.Cast<string>();

    /// <summary>Sugiere IX_{TABLA}_{COL…} mientras el usuario no haya escrito el nombre a mano.</summary>
    private void ActualizarNombre()
    {
        if (_nombreManual) return;
        _sugiriendo = true;
        _txtNombre.Text = _cboTabla.SelectedItem is string tabla && Marcadas.Any() ? DdlTuning.NombreSugerido(tabla, Marcadas) : "";
        _sugiriendo = false;
    }

    /// <summary>Sube o baja la columna elegida conservando su marca.</summary>
    private void Mover(int paso)
    {
        var i = _lstColumnas.SelectedIndex;
        var j = i + paso;
        if (i < 0 || j < 0 || j >= _lstColumnas.Items.Count) return;
        var a = _lstColumnas.Items[i];
        var marcadaA = _lstColumnas.GetItemChecked(i);
        var b = _lstColumnas.Items[j];
        var marcadaB = _lstColumnas.GetItemChecked(j);
        _lstColumnas.Items[i] = b;
        _lstColumnas.SetItemChecked(i, marcadaB);
        _lstColumnas.Items[j] = a;
        _lstColumnas.SetItemChecked(j, marcadaA);
        _lstColumnas.SelectedIndex = j;
        ActualizarNombre();
    }

    private void Aceptar()
    {
        try
        {
            if (_cboEsquema.SelectedItem is not string esquema) throw new ArgumentException("Elija un esquema.");
            if (_cboTabla.SelectedItem is not string tabla) throw new ArgumentException("Elija una tabla.");
            Sql = DdlTuning.CrearIndice(esquema, _txtNombre.Text, tabla, Marcadas.ToList(), _chkUnico.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
    }
}

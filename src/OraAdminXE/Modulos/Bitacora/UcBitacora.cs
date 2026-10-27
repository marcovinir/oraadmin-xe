using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Bitacora;

/// <summary>
/// Bitácora (issue #7): historial de todo lo que se ejecutó desde la app, con filtros por fecha, módulo,
/// resultado y texto; el SQL completo de la fila elegida (con Copiar SQL) y exportación a CSV.
/// </summary>
public partial class UcBitacora : ModuloBase
{
    private const string Todos = "Todos";

    private readonly DateTimePicker _dtpDesde = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _dtpHasta = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly ComboBox _cboModulo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _cboResultado = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly TextBox _txtTexto = new() { Width = 260, PlaceholderText = "Respaldo, Crear usuario…" };
    private readonly BotonPlano _btnBuscar = Tema.Boton("Buscar", primario: true, icono: "");
    private readonly BotonPlano _btnCsv = Tema.Boton("Exportar CSV", icono: "");
    private readonly BotonPlano _btnCopiar = Tema.Boton("Copiar SQL", icono: "");
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtSql = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = Tema.FuenteCodigo
    };
    private readonly Label _lblDetalle = new() { Dock = DockStyle.Fill, Font = Tema.FuenteNegrita, TextAlign = ContentAlignment.MiddleLeft, Text = "SQL de la acción" };
    private readonly Label _lblPie = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private bool _modulosCargados;

    public UcBitacora()
    {
        InitializeComponent();

        _cboResultado.Items.AddRange([Todos, "OK", "ERROR"]);
        _cboResultado.SelectedIndex = 0;
        _cboModulo.Items.Add(Todos);
        _cboModulo.SelectedIndex = 0;
        _dtpDesde.Value = _dtpHasta.Value = DateTime.Today;

        // Fila 1: rango de fechas, módulo y resultado. Fila 2: texto, Buscar y Exportar.
        var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
        filtros.Controls.AddRange(
        [
            Etiqueta("Desde"), _dtpDesde, Etiqueta("Hasta"), _dtpHasta, Etiqueta("Módulo"), _cboModulo, Etiqueta("Resultado"), _cboResultado
        ]);
        var busqueda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        busqueda.Controls.AddRange([Etiqueta("Acción contiene"), _txtTexto, _btnBuscar]);
        foreach (Control c in filtros.Controls.Cast<Control>().Concat(busqueda.Controls.Cast<Control>()))
            c.Margin = c is Label ? new Padding(0, 10, 6, 0) : c is BotonPlano ? new Padding(0, 2, 8, 0) : new Padding(0, 6, 18, 0);
        var exportar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        exportar.Controls.Add(_btnCsv);
        var fila2 = new Panel { Dock = DockStyle.Fill };
        fila2.Controls.Add(busqueda);
        fila2.Controls.Add(exportar);
        var barra = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(0, 4, 0, 8) };
        barra.Controls.Add(fila2);
        barra.Controls.Add(filtros);

        Tema.AplicarGrilla(_grilla);
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grilla.MultiSelect = false;
        _grilla.RowTemplate.Height = 34;
        _grilla.ShowCellToolTips = true;
        Tema.ColumnaEstado(_grilla, "RESULTADO");
        Tema.ColumnaEstado(_grilla, "MODULO", _ => TipoEstado.Acento);
        Tema.MensajeVacio(_grilla, "No hay acciones con esos filtros. Pruebe con otro rango de fechas.", "");
        _grilla.CellFormatting += Grilla_CellFormatting;
        _grilla.SelectionChanged += (_, _) => MostrarSql();

        var tituloSql = new Panel { Dock = DockStyle.Top, Height = 44 };
        var copiar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        copiar.Controls.Add(_btnCopiar);
        tituloSql.Controls.Add(_lblDetalle);
        tituloSql.Controls.Add(copiar);
        var detalle = new Panel { Dock = DockStyle.Bottom, Height = 190, Padding = new Padding(0, 4, 0, 0) };
        detalle.Controls.Add(_txtSql);
        detalle.Controls.Add(tituloSql);

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        pie.Controls.Add(_lblPie);

        pnlCuerpo.BackColor = Tema.Superficie;
        pnlCuerpo.Padding = new Padding(16, 8, 16, 12);
        pnlCuerpo.Controls.Add(_grilla);
        pnlCuerpo.Controls.Add(pie);
        pnlCuerpo.Controls.Add(detalle);
        pnlCuerpo.Controls.Add(barra);

        _btnBuscar.Click += async (_, _) => await BuscarAsync();
        _txtTexto.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await BuscarAsync(); } };
        _btnCsv.Click += (_, _) => { if (_grilla.DataSource is DataTable t) Dialogos.ExportarCsv(this, t, "bitacora"); };
        _btnCopiar.Click += (_, _) => { if (_txtSql.Text.Length > 0) { Clipboard.SetText(_txtSql.Text); Dialogos.Exito(this, "SQL copiado al portapapeles."); } };
        _btnCsv.Enabled = _btnCopiar.Enabled = false;
    }

    public override Modulo Modulo => Modulo.General;

    public override async Task AlActivarAsync()
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        if (!_modulosCargados)
        {
            try
            {
                var modulos = await Bd.ConsultarAsync(SqlBitacora.Modulos);
                foreach (DataRow r in modulos.Rows) _cboModulo.Items.Add((string)r[0]);
                _modulosCargados = true;
            }
            catch (Exception ex) { Dialogos.Error(this, ex); }
        }
        await BuscarAsync();
    }

    private static Label Etiqueta(string texto) => new() { Text = texto, AutoSize = true, ForeColor = Tema.TextoSecundario };

    private async Task BuscarAsync()
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        if (_dtpHasta.Value.Date < _dtpDesde.Value.Date)
        {
            Dialogos.Error(this, new ArgumentException("La fecha «Hasta» no puede ser anterior a «Desde»."));
            return;
        }
        _btnBuscar.Enabled = false;
        UseWaitCursor = true;
        Tema.Cargando(_grilla, true);
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlBitacora.Buscar,
                Parametro.De("desde", _dtpDesde.Value.Date),
                Parametro.De("hasta", _dtpHasta.Value.Date),
                Parametro.De("modulo", _cboModulo.SelectedItem is Todos ? null : _cboModulo.SelectedItem),
                Parametro.De("resultado", _cboResultado.SelectedItem is Todos ? null : _cboResultado.SelectedItem),
                Parametro.De("texto", string.IsNullOrWhiteSpace(_txtTexto.Text) ? null : _txtTexto.Text.Trim()));
            _grilla.DataSource = tabla;
            Encabezados();
            var errores = tabla.AsEnumerable().Count(r => (string)r["RESULTADO"] == "ERROR");
            _lblPie.Text = $"{tabla.Rows.Count} acciones" + (tabla.Rows.Count == SqlBitacora.Maximo ? $" (se muestran las {SqlBitacora.Maximo} más recientes)" : "") +
                           (errores > 0 ? $" · {errores} con ERROR (en rojo)" : "") + " · Elija una fila para ver su SQL.";
            _btnCsv.Enabled = tabla.Rows.Count > 0;
            MostrarSql();
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            Tema.Cargando(_grilla, false);
            _btnBuscar.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void Encabezados()
    {
        var columnas = new Dictionary<string, (string Texto, float Peso)>
        {
            ["FECHA"] = ("Fecha", 13), ["MODULO"] = ("Módulo", 12), ["USUARIO_BD"] = ("Usuario", 10),
            ["ACCION"] = ("Acción", 22), ["RESULTADO"] = ("Resultado", 9), ["MENSAJE"] = ("Mensaje", 34)
        };
        foreach (DataGridViewColumn col in _grilla.Columns)
        {
            if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
            else col.Visible = false;
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd/MM/yy HH:mm:ss";
        }
    }

    /// <summary>Las filas con ERROR se pintan con fondo rojo suave; el mensaje completo va en el tooltip.</summary>
    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView v) return;
        if ((string)v["RESULTADO"] == "ERROR")
        {
            e.CellStyle.BackColor = Tema.PeligroFondo;
            if (_grilla.Columns[e.ColumnIndex].Name == "MENSAJE") e.CellStyle.ForeColor = Tema.Peligro;
        }
        if (_grilla.Columns[e.ColumnIndex].Name is "MENSAJE" or "ACCION" && e.Value is string texto)
        {
            _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
            if (texto.Contains('\n'))
            {
                // Los errores de Oracle traen varias líneas (ORA-39002, ORA-39070…): en la celda van en una sola.
                e.Value = string.Join("  ·  ", texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                e.FormattingApplied = true;
            }
        }
    }

    private void MostrarSql()
    {
        var fila = _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;
        _txtSql.Text = (fila?["SENTENCIA"] as string ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
        _lblDetalle.Text = fila is null ? "SQL de la acción (elija una fila)" : $"SQL de «{fila["ACCION"]}» · {fila["MODULO"]} · {(DateTime)fila["FECHA"]:dd/MM/yyyy HH:mm:ss}";
        _btnCopiar.Enabled = _txtSql.Text.Length > 0;
    }
}

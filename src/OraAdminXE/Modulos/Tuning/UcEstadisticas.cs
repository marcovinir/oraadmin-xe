using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Pestaña Estadísticas (issue #22, P4): estadísticas del optimizador por tabla de un esquema.
/// Recolectar (tabla o esquema) y borrar (destructiva: sirve para mostrar cómo empeora el plan).
/// Las tablas sin analizar o con estadísticas obsoletas se resaltan en amarillo.
/// </summary>
public sealed class UcEstadisticas : UserControl
{
    private readonly ComboBox _cboEsquema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly DataGridView _grd = new() { Dock = DockStyle.Fill };
    private readonly Label _lblPie = new() { Dock = DockStyle.Bottom, Height = 30, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnTabla = Tema.Boton("Recolectar (tabla)", primario: true);
    private readonly BotonPlano _btnEsquema = Tema.Boton("Recolectar (esquema)");
    private readonly BotonPlano _btnBorrar = Tema.Boton("Borrar estadísticas (tabla)");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar");

    private bool _cargando;
    private bool _ocupado;
    private int _version;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcEstadisticas()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnBorrar.Estilo = EstiloBoton.Peligro;

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        barra.Controls.Add(new Label { Text = "Esquema", AutoSize = true, Margin = new Padding(0, 9, 8, 0), ForeColor = Tema.TextoSecundario });
        barra.Controls.Add(_cboEsquema);
        barra.Controls.AddRange([_btnTabla, _btnEsquema, _btnBorrar, _btnRefrescar]);

        Controls.Add(_grd);
        Controls.Add(_lblPie);
        Controls.Add(barra);

        Tema.AplicarGrilla(_grd);
        Tema.MensajeVacio(_grd, "El esquema no tiene tablas", "\uE9D9");
        _grd.MultiSelect = false;
        _grd.CellFormatting += Grd_CellFormatting;
        _grd.SelectionChanged += (_, _) => ActualizarBotones();

        _cboEsquema.SelectedIndexChanged += async (_, _) => { if (!_cargando) await CargarEstadisticasAsync(); };
        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnTabla.Click += async (_, _) => await RecolectarTablaAsync();
        _btnEsquema.Click += async (_, _) => await RecolectarEsquemaAsync();
        _btnBorrar.Click += async (_, _) => await BorrarAsync();
        ActualizarBotones();
    }

    private bool PuedeActualizarUi => !IsDisposed && !Disposing;
    private string? Esquema => _cboEsquema.SelectedItem as string;
    private string? Tabla => _grd.CurrentRow?.DataBoundItem is DataRowView f ? f.Row.Field<string>("TABLE_NAME") : null;

    /// <summary>Recarga la lista de esquemas (conservando el elegido) y las estadísticas.</summary>
    public async Task CargarAsync()
    {
        if (_cargando || !PuedeActualizarUi || string.IsNullOrEmpty(Bd.Usuario)) return;
        _cargando = true;
        ++_version;
        Tema.Cargando(_grd, false);
        _grd.DataSource = null;
        _lblPie.Text = "";
        _cboEsquema.Enabled = false;
        ActualizarBotones();
        try
        {
            var anterior = Esquema;
            var esquemas = (await Bd.ConsultarAsync(SqlTuning.Esquemas)).AsEnumerable().Select(r => r.Field<string>("USERNAME")!).ToArray();
            if (!PuedeActualizarUi) return;
            _cboEsquema.Items.Clear();
            _cboEsquema.Items.AddRange(esquemas.ToArray<object>());
            _cboEsquema.SelectedItem = esquemas.Contains(anterior) ? anterior
                : esquemas.Contains("DEMO_VENTAS") ? "DEMO_VENTAS" : esquemas.FirstOrDefault();
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi) Dialogos.Error(this, ex);
            return;
        }
        finally
        {
            _cargando = false;
            if (PuedeActualizarUi) { _cboEsquema.Enabled = true; ActualizarBotones(); }
        }
        await CargarEstadisticasAsync();
    }

    private async Task CargarEstadisticasAsync(string? seleccionar = null)
    {
        var version = ++_version;
        try
        {
            // Se vacía la grilla ya: si quedaran filas del esquema anterior, una acción podría mezclarlas con el nuevo.
            _grd.DataSource = null;
            _lblPie.Text = "";
            ActualizarBotones();
            if (Esquema is not { } esquema) return;
            Tema.Cargando(_grd, true);
            var tabla = await Bd.ConsultarAsync(SqlTuning.Estadisticas, Parametro.De("e", esquema));
            if (version != _version || !PuedeActualizarUi) return;
            _grd.DataSource = tabla;
            var columnas = new Dictionary<string, (string Texto, float Peso)>
            {
                ["TABLE_NAME"] = ("Tabla", 28), ["NUM_ROWS"] = ("Filas", 12), ["BLOCKS"] = ("Bloques", 12),
                ["AVG_ROW_LEN"] = ("Long. media de fila", 14), ["LAST_ANALYZED"] = ("Último análisis", 20), ["STALE_STATS"] = ("Obsoletas", 14)
            };
            foreach (DataGridViewColumn col in _grd.Columns)
                if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
                else col.Visible = false;

            var fila = _grd.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => (string?)f.Cells["TABLE_NAME"].Value == seleccionar);
            if (fila is not null)
            {
                _grd.ClearSelection();
                fila.Selected = true;
                _grd.CurrentCell = fila.Cells["TABLE_NAME"];
            }
            var pendientes = tabla.AsEnumerable().Count(Resaltar);
            _lblPie.Text = pendientes == 0
                ? $"{tabla.Rows.Count} tablas, todas con estadísticas al día."
                : $"{tabla.Rows.Count} tablas · {pendientes} sin analizar u obsoletas (en amarillo): el optimizador puede elegir un mal plan.";
        }
        catch (Exception ex)
        {
            if (version == _version && PuedeActualizarUi) Dialogos.Error(this, ex);
        }
        finally
        {
            if (version == _version && PuedeActualizarUi)
            {
                Tema.Cargando(_grd, false);
                ActualizarBotones();
            }
        }
    }

    /// <summary>Sin fecha de análisis o marcada como obsoleta.</summary>
    private static bool Resaltar(DataRow fila) =>
        fila["LAST_ANALYZED"] is DBNull || string.Equals(fila["STALE_STATS"] as string, "YES", StringComparison.OrdinalIgnoreCase);

    private void Grd_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grd.Rows[e.RowIndex].DataBoundItem is not DataRowView v || !Resaltar(v.Row)) return;
        e.CellStyle.ForeColor = Tema.Advertencia;
        e.CellStyle.BackColor = Tema.AdvertenciaFondo;
    }

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario) && !_ocupado && !_cargando;
        _btnRefrescar.Enabled = conSesion;
        _btnEsquema.Enabled = conSesion && Esquema is not null;
        _btnTabla.Enabled = _btnBorrar.Enabled = conSesion && Tabla is not null;
    }

    private async Task RecolectarTablaAsync()
    {
        if (Esquema is not { } e || Tabla is not { } t) return;
        await EjecutarAsync("Recolectar estadísticas de tabla", DdlTuning.RecolectarTabla(e, t), false, $"Estadísticas de {e}.{t} recolectadas.", t);
    }

    private async Task RecolectarEsquemaAsync()
    {
        if (Esquema is not { } e) return;
        await EjecutarAsync("Recolectar estadísticas de esquema", DdlTuning.RecolectarEsquema(e), false, $"Estadísticas del esquema {e} recolectadas.", Tabla);
    }

    private async Task BorrarAsync()
    {
        if (Esquema is not { } e || Tabla is not { } t) return;
        await EjecutarAsync("Borrar estadísticas de tabla", DdlTuning.BorrarTabla(e, t), true, $"Estadísticas de {e}.{t} borradas.", t);
    }

    private async Task EjecutarAsync(string accion, Sentencia sentencia, bool destructiva, string exito, string? seleccionar)
    {
        try
        {
            if (!Dialogos.ConfirmarSql(this, accion, sentencia.Visible, destructiva)) return;
            _ocupado = true;
            ActualizarBotones();
            UseWaitCursor = true;
            try
            {
                await Bd.EjecutarAsync(Modulo.Tuning, accion, sentencia.Sql, sentencia.Parametros);
                Dialogos.Exito(this, exito);
            }
            finally
            {
                _ocupado = false;
                if (PuedeActualizarUi) UseWaitCursor = false;
                await CargarEstadisticasAsync(seleccionar);
            }
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi) Dialogos.Error(this, ex);
        }
    }
}

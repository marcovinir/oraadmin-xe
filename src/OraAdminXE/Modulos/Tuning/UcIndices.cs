using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Pestaña Índices (issue #22, P4): índices de un esquema con sus columnas, estado y altura (BLEVEL).
/// Crear, reconstruir y eliminar. Los índices que sostienen una PK o UK no se pueden eliminar.
/// </summary>
public sealed class UcIndices : UserControl
{
    private readonly ComboBox _cboEsquema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly DataGridView _grd = new() { Dock = DockStyle.Fill };
    private readonly Label _lblPie = new() { Dock = DockStyle.Bottom, Height = 30, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnCrear = Tema.Boton("Crear índice…", primario: true);
    private readonly BotonPlano _btnReconstruir = Tema.Boton("Reconstruir");
    private readonly BotonPlano _btnEliminar = Tema.Boton("Eliminar");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar");

    private bool _cargando;
    private bool _ocupado;
    private int _version;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcIndices()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnEliminar.Estilo = EstiloBoton.Peligro;

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        barra.Controls.Add(new Label { Text = "Esquema", AutoSize = true, Margin = new Padding(0, 9, 8, 0), ForeColor = Tema.TextoSecundario });
        barra.Controls.Add(_cboEsquema);
        barra.Controls.AddRange([_btnCrear, _btnReconstruir, _btnEliminar, _btnRefrescar]);

        Controls.Add(_grd);
        Controls.Add(_lblPie);
        Controls.Add(barra);

        Tema.AplicarGrilla(_grd);
        Tema.MensajeVacio(_grd, "El esquema no tiene índices", "\uE9D9");
        _grd.MultiSelect = false;
        Tema.ColumnaEstado(_grd, "STATUS");
        Tema.ColumnaEstado(_grd, "RESPALDA", _ => TipoEstado.Info);
        _grd.SelectionChanged += (_, _) => ActualizarBotones();

        _cboEsquema.SelectedIndexChanged += async (_, _) => { if (!_cargando) await CargarIndicesAsync(); };
        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnCrear.Click += async (_, _) => await CrearAsync();
        _btnReconstruir.Click += async (_, _) => await ReconstruirAsync();
        _btnEliminar.Click += async (_, _) => await EliminarAsync();
        ActualizarBotones();
    }

    private bool PuedeActualizarUi => !IsDisposed && !Disposing;
    private string? Esquema => _cboEsquema.SelectedItem as string;
    private DataRow? Fila => (_grd.CurrentRow?.DataBoundItem as DataRowView)?.Row;
    private string? Indice => Fila?.Field<string>("INDEX_NAME");
    private bool Protegido => Fila is { } f && f["RESPALDA"] is string s && s.Length > 0;

    /// <summary>Recarga la lista de esquemas (conservando el elegido) y los índices.</summary>
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
            var esquemas = await EsquemasAsync();
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
        await CargarIndicesAsync();
    }

    private static async Task<string[]> EsquemasAsync() =>
        (await Bd.ConsultarAsync(SqlTuning.Esquemas)).AsEnumerable().Select(r => r.Field<string>("USERNAME")!).ToArray();

    private async Task CargarIndicesAsync(string? seleccionar = null)
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
            var tabla = await Bd.ConsultarAsync(SqlTuning.Indices, Parametro.De("e", esquema));
            if (version != _version || !PuedeActualizarUi) return;
            _grd.DataSource = tabla;
            var columnas = new Dictionary<string, (string Texto, float Peso)>
            {
                ["INDEX_NAME"] = ("Índice", 22), ["TABLE_NAME"] = ("Tabla", 18), ["COLUMNAS"] = ("Columnas", 24),
                ["UNIQUENESS"] = ("Unicidad", 10), ["STATUS"] = ("Estado", 9), ["BLEVEL"] = ("BLEVEL", 7),
                ["DISTINCT_KEYS"] = ("Claves distintas", 11), ["RESPALDA"] = ("Respalda", 9)
            };
            foreach (DataGridViewColumn col in _grd.Columns)
                if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
                else col.Visible = false;

            var fila = _grd.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => (string?)f.Cells["INDEX_NAME"].Value == seleccionar);
            if (fila is not null)
            {
                _grd.ClearSelection();
                fila.Selected = true;
                _grd.CurrentCell = fila.Cells["INDEX_NAME"];
            }
            _lblPie.Text = $"{tabla.Rows.Count} índices · «PK/UK» marca los que sostienen una clave primaria o única y no se pueden eliminar.";
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

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario) && !_ocupado && !_cargando;
        _btnRefrescar.Enabled = _btnCrear.Enabled = conSesion;
        _btnReconstruir.Enabled = conSesion && Indice is not null;
        _btnEliminar.Enabled = conSesion && Indice is not null && !Protegido;
    }

    private async Task CrearAsync()
    {
        try
        {
            var esquemas = await EsquemasAsync();
            using var dlg = new DlgIndice(esquemas, Esquema,
                async e => (await Bd.ConsultarAsync(SqlTuning.TablasDe, Parametro.De("e", e))).AsEnumerable().Select(r => r.Field<string>("TABLE_NAME")!).ToArray(),
                async (e, t) => (await Bd.ConsultarAsync(SqlTuning.ColumnasDe, Parametro.De("e", e), Parametro.De("t", t))).AsEnumerable().Select(r => r.Field<string>("COLUMN_NAME")!).ToArray());
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await EjecutarAsync("Crear índice", dlg.Sql, false, "Índice creado.", null);
        }
        catch (Exception ex) { if (PuedeActualizarUi) Dialogos.Error(this, ex); }
    }

    private static string NombreIndiceSql(string ix) =>
        System.Text.RegularExpressions.Regex.IsMatch(ix, "^[A-Z][A-Z0-9_$#]{0,127}$")
            ? ix
            : "\"" + ix.Replace("\"", "\"\"") + "\"";

    private async Task ReconstruirAsync()
    {
        if (Esquema is not { } e || Indice is not { } ix) return;
        try
        {
            var ixSql = NombreIndiceSql(ix);
            await EjecutarAsync("Reconstruir índice", DdlTuning.ReconstruirIndice(e, ixSql), false, $"Índice {ix} reconstruido.", ix);
        }
        catch (Exception ex) { if (PuedeActualizarUi) Dialogos.Error(this, ex); }
    }

    private async Task EliminarAsync()
    {
        if (Esquema is not { } e || Indice is not { } ix) return;
        try
        {
            if (Protegido)
                throw new InvalidOperationException($"{ix} sostiene una clave primaria o única: se elimina quitando la restricción, no el índice.");
            var ixSql = NombreIndiceSql(ix);
            await EjecutarAsync("Eliminar índice", DdlTuning.EliminarIndice(e, ixSql), true, $"Índice {ix} eliminado.", null);
        }
        catch (Exception ex) { if (PuedeActualizarUi) Dialogos.Error(this, ex); }
    }

    /// <summary>Muestra el SQL, pide confirmación, ejecuta (queda en la bitácora) y recarga la grilla.</summary>
    private async Task EjecutarAsync(string accion, string sql, bool destructiva, string exito, string? seleccionar)
    {
        if (!Dialogos.ConfirmarSql(this, accion, sql + ";", destructiva)) return;
        _ocupado = true;
        ActualizarBotones();
        UseWaitCursor = true;
        try
        {
            await Bd.EjecutarAsync(Modulo.Tuning, accion, sql);
            Dialogos.Exito(this, exito);
        }
        finally
        {
            _ocupado = false;
            if (PuedeActualizarUi) UseWaitCursor = false;
            await CargarIndicesAsync(seleccionar);
        }
    }
}

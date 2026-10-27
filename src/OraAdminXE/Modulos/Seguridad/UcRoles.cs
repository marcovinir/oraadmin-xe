using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Pestaña Roles y privilegios de Seguridad (issue #9). A la izquierda los roles (crear / eliminar);
/// a la derecha todo lo que tiene un usuario o rol, con Otorgar y Revocar. Los roles de Oracle son de solo lectura.
/// </summary>
public sealed class UcRoles : UserControl
{
    private sealed record Beneficiario(string Nombre, string Tipo, bool DeOracle)
    {
        public override string ToString() => $"{Nombre}  ({(Tipo == "ROL" ? "rol" : "usuario")}{(DeOracle ? " de Oracle" : "")})";
    }

    private readonly DataGridView _grdRoles = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _grdPrivilegios = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cboBeneficiario = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Margin = new Padding(0, 8, 8, 0) };
    private readonly CheckBox _chkOracle = new() { Text = "Mostrar roles de Oracle", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 6, 0, 0) };
    private readonly Label _lblPie = new() { Dock = DockStyle.Bottom, Height = 30, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _btnCrearRol = Tema.Boton("＋  Crear rol", primario: true);
    private readonly Button _btnEliminarRol = Tema.Boton("Eliminar rol");
    private readonly Button _btnOtorgar = Tema.Boton("Otorgar…", primario: true);
    private readonly Button _btnRevocar = Tema.Boton("Revocar");
    private readonly Button _btnRefrescar = Tema.Boton("⟳  Refrescar");
    private bool _cargando;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcRoles()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnEliminarRol.ForeColor = _btnRevocar.ForeColor = Tema.Peligro;

        // Izquierda: roles
        var barraRoles = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        barraRoles.Controls.AddRange([_btnCrearRol, _btnEliminarRol]);
        var izquierda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
        izquierda.Controls.Add(_grdRoles);
        izquierda.Controls.Add(_chkOracle);
        izquierda.Controls.Add(barraRoles);
        izquierda.Controls.Add(Titulo("Roles"));

        // Derecha: privilegios del beneficiario elegido
        var selector = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false };
        selector.Controls.AddRange([new Label { Text = "Privilegios de", AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), Margin = new Padding(0, 9, 8, 0) }, _cboBeneficiario]);
        var barraPriv = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        barraPriv.Controls.AddRange([_btnOtorgar, _btnRevocar, _btnRefrescar]);
        var derecha = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        derecha.Controls.Add(_grdPrivilegios);
        derecha.Controls.Add(barraPriv);
        derecha.Controls.Add(selector);

        var division = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        division.Controls.Add(izquierda, 0, 0);
        division.Controls.Add(derecha, 1, 0);

        foreach (var g in new[] { _grdRoles, _grdPrivilegios })
        {
            Tema.AplicarGrilla(g);
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.MultiSelect = false;
            g.RowTemplate.Height = 30;
            g.SelectionChanged += (_, _) => ActualizarBotones();
        }
        _grdRoles.CellFormatting += GrdRoles_CellFormatting;
        _grdPrivilegios.CellFormatting += GrdPrivilegios_CellFormatting;

        Controls.Add(division);
        Controls.Add(_lblPie);

        _grdRoles.CellClick += async (_, e) => { if (e.RowIndex >= 0) await VerPrivilegiosDelRolAsync(); };
        _cboBeneficiario.SelectedIndexChanged += async (_, _) => { if (!_cargando) await CargarPrivilegiosAsync(); };
        _chkOracle.CheckedChanged += async (_, _) => await CargarAsync();
        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnCrearRol.Click += async (_, _) => await CrearRolAsync();
        _btnEliminarRol.Click += async (_, _) => await EliminarRolAsync();
        _btnOtorgar.Click += async (_, _) => await OtorgarAsync();
        _btnRevocar.Click += async (_, _) => await RevocarAsync();
        ActualizarBotones();
    }

    private static Label Titulo(string texto) =>
        new() { Text = texto, Dock = DockStyle.Top, Height = 44, Font = new Font("Segoe UI Semibold", 12f), TextAlign = ContentAlignment.MiddleLeft };

    private static DataRow? Fila(DataGridView g) =>
        g.SelectedRows.Count > 0 && g.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;

    private Beneficiario? Actual => _cboBeneficiario.SelectedItem as Beneficiario;

    /// <summary>Recarga roles, la lista de beneficiarios y los privilegios del beneficiario elegido.</summary>
    public async Task CargarAsync(string? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        UseWaitCursor = true;
        _cargando = true;
        try
        {
            var roles = Bd.ConsultarAsync(SqlSeguridad.Roles, Parametro.De("todos", _chkOracle.Checked ? 1 : 0));
            var beneficiarios = Bd.ConsultarAsync(SqlSeguridad.Beneficiarios);
            await Task.WhenAll(roles, beneficiarios);

            _grdRoles.DataSource = roles.Result;
            Encabezados(_grdRoles, new() { ["ROLE"] = ("Rol", 70), ["OTORGADO_A"] = ("Otorgado a", 30) });

            var anterior = seleccionar ?? Actual?.Nombre;
            _cboBeneficiario.Items.Clear();
            foreach (DataRow r in beneficiarios.Result.Rows)
                _cboBeneficiario.Items.Add(new Beneficiario((string)r["NOMBRE"], (string)r["TIPO"], false));
            Elegir(anterior);
            var propios = roles.Result.Select("ORACLE_MAINTAINED = 'N'").Length;
            _lblPie.Text = $"{propios} {(propios == 1 ? "rol propio" : "roles propios")} · Haga clic en un rol para ver sus privilegios. " +
                           "Los roles de Oracle (en gris) son de solo lectura.";
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            _cargando = false;
            UseWaitCursor = false;
        }
        await CargarPrivilegiosAsync();
    }

    private void Elegir(string? nombre)
    {
        var item = _cboBeneficiario.Items.Cast<Beneficiario>().FirstOrDefault(b => b.Nombre == nombre)
                   ?? _cboBeneficiario.Items.Cast<Beneficiario>().FirstOrDefault();
        _cboBeneficiario.SelectedItem = item;
    }

    private async Task VerPrivilegiosDelRolAsync()
    {
        if (Fila(_grdRoles) is not { } fila) return;
        var rol = (string)fila["ROLE"];
        var deOracle = (string)fila["ORACLE_MAINTAINED"] == "Y";
        if (deOracle && !_cboBeneficiario.Items.Cast<Beneficiario>().Any(b => b.Nombre == rol))
            _cboBeneficiario.Items.Add(new Beneficiario(rol, "ROL", true));   // solo para consultar
        _cargando = true;
        Elegir(rol);
        _cargando = false;
        await CargarPrivilegiosAsync();
    }

    private async Task CargarPrivilegiosAsync()
    {
        if (Actual is not { } b) { _grdPrivilegios.DataSource = null; ActualizarBotones(); return; }
        try
        {
            _grdPrivilegios.DataSource = await Bd.ConsultarAsync(SqlSeguridad.PrivilegiosDe, Parametro.De("g", b.Nombre));
            Encabezados(_grdPrivilegios, new()
            {
                ["TIPO"] = ("Tipo", 14), ["PRIVILEGIO"] = ("Privilegio o rol", 36), ["OBJETO"] = ("Objeto", 34), ["CON_OPCION"] = ("Reotorgable", 16)
            });
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        ActualizarBotones();
    }

    private static void Encabezados(DataGridView g, Dictionary<string, (string Texto, float Peso)> columnas)
    {
        foreach (DataGridViewColumn col in g.Columns)
        {
            if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
            else col.Visible = false;
        }
    }

    private void GrdRoles_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && e.CellStyle is not null && _grdRoles.Rows[e.RowIndex].DataBoundItem is DataRowView v && (string)v["ORACLE_MAINTAINED"] == "Y")
            e.CellStyle.ForeColor = Tema.TextoSecundario;
    }

    private void GrdPrivilegios_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grdPrivilegios.Columns[e.ColumnIndex].Name != "TIPO" || e.Value is not string tipo) return;
        e.CellStyle.ForeColor = tipo switch { "ROL" => Tema.Acento, "SISTEMA" => Color.FromArgb(37, 99, 235), _ => Color.FromArgb(13, 148, 136) };
        e.CellStyle.Font = new Font(Tema.Fuente, FontStyle.Bold);
    }

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario);
        var rol = Fila(_grdRoles);
        _btnCrearRol.Enabled = conSesion;
        _btnEliminarRol.Enabled = rol is not null && (string)rol["ORACLE_MAINTAINED"] == "N";
        var editable = Actual is { DeOracle: false };
        _btnOtorgar.Enabled = editable;
        _btnRevocar.Enabled = editable && Fila(_grdPrivilegios) is not null;
    }

    private async Task CrearRolAsync()
    {
        var nombre = Pedir("Crear rol", "Nombre del rol nuevo (por ejemplo ROL_CONSULTA):");
        if (nombre is null) return;
        try { await EjecutarAsync("Crear rol", DdlRoles.CrearRol(nombre), false, $"Rol {nombre.ToUpperInvariant()} creado.", nombre.ToUpperInvariant()); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EliminarRolAsync()
    {
        if (Fila(_grdRoles)?["ROLE"] is not string rol) return;
        try { await EjecutarAsync("Eliminar rol", DdlRoles.EliminarRol(rol), true, $"Rol {rol} eliminado.", null); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task OtorgarAsync()
    {
        if (Actual is not { DeOracle: false } b) return;
        try
        {
            var roles = Bd.ConsultarAsync(SqlSeguridad.TodosLosRoles);
            var sistema = Bd.ConsultarAsync(SqlSeguridad.PrivilegiosSistema);
            var esquemas = Bd.ConsultarAsync(SqlSeguridad.Esquemas);
            await Task.WhenAll(roles, sistema, esquemas);
            static string[] Lista(DataTable t) => t.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToArray();

            using var dlg = new DlgOtorgar(b.Nombre, Lista(roles.Result), Lista(sistema.Result), Lista(esquemas.Result),
                async esquema => Lista(await Bd.ConsultarAsync(SqlSeguridad.TablasDe, Parametro.De("e", esquema))));
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await EjecutarAsync("Otorgar privilegio", dlg.Sql, false, $"Otorgado a {b.Nombre}.", b.Nombre);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task RevocarAsync()
    {
        if (Actual is not { DeOracle: false } b || Fila(_grdPrivilegios) is not { } fila) return;
        try
        {
            var sql = DdlRoles.Revocar((string)fila["TIPO"], (string)fila["PRIVILEGIO"], fila["OBJETO"] as string, b.Nombre);
            await EjecutarAsync("Revocar privilegio", sql, true, $"Revocado a {b.Nombre}.", b.Nombre);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EjecutarAsync(string titulo, string sql, bool destructiva, string exito, string? seleccionar)
    {
        if (!Dialogos.ConfirmarSql(this, titulo, sql + ";", destructiva)) return;
        try
        {
            await Bd.EjecutarAsync(Modulo.Seguridad, titulo, sql);
            Dialogos.Exito(this, exito);
        }
        finally
        {
            await CargarAsync(seleccionar);
        }
    }

    /// <summary>Pide un texto corto (nombre del rol).</summary>
    private string? Pedir(string titulo, string pregunta)
    {
        using var f = new Form
        {
            Text = titulo, Font = Tema.Fuente, BackColor = Tema.Superficie, FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            ClientSize = new Size(420, 150)
        };
        var lbl = new Label { Text = pregunta, AutoSize = true, Location = new Point(20, 18) };
        var txt = new TextBox { CharacterCasing = CharacterCasing.Upper, Location = new Point(20, 46), Width = 380 };
        var ok = new Button { Text = "Crear", DialogResult = DialogResult.OK, Location = new Point(290, 96), Size = new Size(110, 36), BackColor = Tema.Acento, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        ok.FlatAppearance.BorderSize = 0;
        var cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(170, 96), Size = new Size(110, 36) };
        f.Controls.AddRange([lbl, txt, ok, cancelar]);
        f.AcceptButton = ok;
        f.CancelButton = cancelar;
        return f.ShowDialog(this) == DialogResult.OK && txt.Text.Trim().Length > 0 ? txt.Text.Trim() : null;
    }
}

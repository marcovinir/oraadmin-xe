using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Pestaña Perfiles de Seguridad (issue #10, P2). A la izquierda los perfiles y cuántos usuarios los usan;
/// a la derecha los límites del perfil elegido. Crear, editar, eliminar y asignar a un usuario.
/// DEFAULT y los perfiles de Oracle (ORA_*) son de solo lectura.
/// </summary>
public sealed class UcPerfiles : UserControl
{
    private readonly DataGridView _grdPerfiles = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _grdLimites = new() { Dock = DockStyle.Fill };
    private readonly Label _lblLimites = new() { Dock = DockStyle.Top, Height = 44, Font = new Font("Segoe UI Semibold", 12f), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblPie = new() { Dock = DockStyle.Bottom, Height = 30, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnCrear = Tema.Boton("Crear perfil", primario: true, icono: "");
    private readonly BotonPlano _btnEliminar = Tema.Boton("Eliminar", icono: "");
    private readonly BotonPlano _btnEditar = Tema.Boton("Editar límites…", icono: "");
    private readonly BotonPlano _btnAsignar = Tema.Boton("Asignar a usuario…", icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");

    private static IBaseDatos Bd => Sesion.Bd;

    public UcPerfiles()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnEliminar.Estilo = EstiloBoton.Peligro;

        var barraPerfiles = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        barraPerfiles.Controls.AddRange([_btnCrear, _btnEliminar]);
        var izquierda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
        izquierda.Controls.Add(_grdPerfiles);
        izquierda.Controls.Add(barraPerfiles);
        izquierda.Controls.Add(new Label { Text = "Perfiles", Dock = DockStyle.Top, Height = 44, Font = new Font("Segoe UI Semibold", 12f), TextAlign = ContentAlignment.MiddleLeft });

        var barraLimites = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        barraLimites.Controls.AddRange([_btnEditar, _btnAsignar, _btnRefrescar]);
        var derecha = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        derecha.Controls.Add(_grdLimites);
        derecha.Controls.Add(barraLimites);
        derecha.Controls.Add(_lblLimites);

        var division = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        division.Controls.Add(izquierda, 0, 0);
        division.Controls.Add(derecha, 1, 0);

        foreach (var g in new[] { _grdPerfiles, _grdLimites })
        {
            Tema.AplicarGrilla(g);
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.MultiSelect = false;
            g.RowTemplate.Height = 34;
        }
        Tema.ColumnaEstado(_grdLimites, "RESOURCE_TYPE", t => t == "PASSWORD" ? TipoEstado.Acento : TipoEstado.Info);
        Tema.ColumnaEstado(_grdLimites, "VALOR", t => t switch { "UNLIMITED" => TipoEstado.Advertencia, "DEFAULT" => TipoEstado.Neutro, _ => TipoEstado.Ok });
        Tema.MensajeVacio(_grdPerfiles, "No hay perfiles.", "");
        Tema.MensajeVacio(_grdLimites, "Elija un perfil para ver sus límites.", "");
        _grdPerfiles.CellFormatting += GrdPerfiles_CellFormatting;
        _grdPerfiles.SelectionChanged += async (_, _) => await CargarLimitesAsync();

        Controls.Add(division);
        Controls.Add(_lblPie);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnCrear.Click += async (_, _) => await CrearAsync();
        _btnEditar.Click += async (_, _) => await EditarAsync();
        _btnEliminar.Click += async (_, _) => await EliminarAsync();
        _btnAsignar.Click += async (_, _) => await AsignarAsync();
        ActualizarBotones();
    }

    private string? Perfil =>
        _grdPerfiles.SelectedRows.Count > 0 && _grdPerfiles.SelectedRows[0].DataBoundItem is DataRowView v ? (string)v["PROFILE"] : null;

    public async Task CargarAsync(string? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        var anterior = seleccionar ?? Perfil;
        UseWaitCursor = true;
        try
        {
            var perfiles = await Bd.ConsultarAsync(SqlPerfiles.Perfiles);
            _grdPerfiles.DataSource = perfiles;
            foreach (DataGridViewColumn col in _grdPerfiles.Columns)
                (col.HeaderText, col.FillWeight) = col.Name switch { "PROFILE" => ("Perfil", 70f), "USUARIOS" => ("Usuarios", 30f), _ => (col.HeaderText, col.FillWeight) };
            var fila = _grdPerfiles.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => (string)f.Cells["PROFILE"].Value == anterior)
                       ?? _grdPerfiles.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => !DdlPerfiles.EsProtegido((string)f.Cells["PROFILE"].Value))
                       ?? _grdPerfiles.Rows.Cast<DataGridViewRow>().FirstOrDefault();
            if (fila is not null)
            {
                _grdPerfiles.ClearSelection();
                fila.Selected = true;
                _grdPerfiles.CurrentCell = fila.Cells["PROFILE"];
            }
            var propios = perfiles.AsEnumerable().Count(r => !DdlPerfiles.EsProtegido((string)r["PROFILE"]));
            _lblPie.Text = $"{propios} {(propios == 1 ? "perfil propio" : "perfiles propios")} · DEFAULT y los perfiles de Oracle (en gris) son de solo lectura. " +
                           "Los límites de recursos (KERNEL) aplican porque RESOURCE_LIMIT = TRUE.";
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
        await CargarLimitesAsync();
    }

    private async Task CargarLimitesAsync()
    {
        ActualizarBotones();
        if (Perfil is not { } perfil)
        {
            _grdLimites.DataSource = null;
            _lblLimites.Text = "Límites";
            return;
        }
        try
        {
            var limites = await Bd.ConsultarAsync(SqlPerfiles.LimitesDe, Parametro.De("p", perfil));
            limites.Columns.Add("DESCRIPCION", typeof(string));
            limites.Columns.Add("VALOR", typeof(string));
            foreach (DataRow r in limites.Rows)
            {
                r["DESCRIPCION"] = DdlPerfiles.Descripciones.GetValueOrDefault((string)r["RESOURCE_NAME"], "");
                r["VALOR"] = DdlPerfiles.ValorLegible((string)r["RESOURCE_NAME"], (string)r["LIMIT"]);
            }
            _grdLimites.DataSource = limites;
            var columnas = new Dictionary<string, (string Texto, float Peso)>
            {
                ["RESOURCE_NAME"] = ("Límite", 30), ["DESCRIPCION"] = ("Qué controla", 44), ["RESOURCE_TYPE"] = ("Tipo", 12), ["VALOR"] = ("Valor", 14)
            };
            foreach (DataGridViewColumn col in _grdLimites.Columns)
                if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
                else col.Visible = false;
            var orden = 0;
            foreach (var nombre in columnas.Keys) _grdLimites.Columns[nombre]!.DisplayIndex = orden++;
            _lblLimites.Text = $"Límites de {perfil}" + (DdlPerfiles.EsProtegido(perfil) ? "  (solo lectura)" : "");
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private void GrdPerfiles_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && e.CellStyle is not null && _grdPerfiles.Rows[e.RowIndex].DataBoundItem is DataRowView v && DdlPerfiles.EsProtegido((string)v["PROFILE"]))
            e.CellStyle.ForeColor = Tema.TextoSecundario;
    }

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario);
        var editable = Perfil is { } p && !DdlPerfiles.EsProtegido(p);
        _btnCrear.Enabled = _btnRefrescar.Enabled = conSesion;
        _btnEditar.Enabled = _btnEliminar.Enabled = editable;
        _btnAsignar.Enabled = conSesion && Perfil is not null;
    }

    private async Task<Dictionary<string, LimitePerfil>> LimitesActualesAsync(string perfil)
    {
        var tabla = await Bd.ConsultarAsync(SqlPerfiles.LimitesDe, Parametro.De("p", perfil));
        return tabla.AsEnumerable().ToDictionary(r => (string)r["RESOURCE_NAME"], r => DdlPerfiles.Leer((string)r["RESOURCE_NAME"], (string)r["LIMIT"]));
    }

    private async Task CrearAsync()
    {
        try
        {
            using var dlg = new DlgPerfil(null, null);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var perfil = Identificador.Validar(dlg.Perfil, "perfil");
            await EjecutarAsync("Crear perfil", DdlPerfiles.Crear(perfil, dlg.Limites), false, $"Perfil {perfil} creado.", perfil);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EditarAsync()
    {
        if (Perfil is not { } perfil) return;
        try
        {
            using var dlg = new DlgPerfil(perfil, await LimitesActualesAsync(perfil));
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await EjecutarAsync("Modificar perfil", DdlPerfiles.Modificar(perfil, dlg.Limites), false, $"Perfil {perfil} actualizado.", perfil);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EliminarAsync()
    {
        if (Perfil is not { } perfil) return;
        try { await EjecutarAsync("Eliminar perfil", DdlPerfiles.Eliminar(perfil), true, $"Perfil {perfil} eliminado (sus usuarios pasan a DEFAULT).", null); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task AsignarAsync()
    {
        if (Perfil is not { } perfil) return;
        try
        {
            var usuarios = (await Bd.ConsultarAsync(SqlPerfiles.Usuarios)).AsEnumerable()
                .Where(r => !string.Equals((string)r["USERNAME"], Bd.Usuario, StringComparison.OrdinalIgnoreCase))
                .Select(r => ((string)r["USERNAME"], (string)r["PROFILE"])).ToList();
            if (usuarios.Count == 0)
                throw new InvalidOperationException("No hay usuarios propios a los que asignar el perfil. Créelos en la pestaña Usuarios.");
            var usuario = ElegirUsuario(perfil, usuarios);
            if (usuario is null) return;
            await EjecutarAsync("Asignar perfil", DdlPerfiles.Asignar(usuario, perfil), false, $"{usuario} ahora usa el perfil {perfil}.", perfil);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    /// <summary>Elige el usuario al que se le asigna el perfil (no se ofrece el de la sesión, para no bloquearse).</summary>
    private string? ElegirUsuario(string perfil, IReadOnlyList<(string Usuario, string Perfil)> usuarios)
    {
        using var f = new VentanaBase
        {
            Text = $"Asignar el perfil {perfil}", BackColor = Tema.Superficie, FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            ClientSize = new Size(440, 170)
        };
        var lbl = new Label { Text = "Usuario (entre paréntesis, su perfil actual):", AutoSize = true, Location = new Point(20, 18) };
        var cbo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(20, 46), Width = 400 };
        foreach (var (u, p) in usuarios) cbo.Items.Add($"{u}  ({p})");
        cbo.SelectedIndex = Math.Max(0, usuarios.ToList().FindIndex(x => x.Usuario == "JUAN"));
        var ok = new BotonPlano { Text = "Asignar", Estilo = EstiloBoton.Primario, DialogResult = DialogResult.OK, AutoSize = false, Location = new Point(310, 112), Size = new Size(110, 36) };
        var cancelar = new BotonPlano { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = false, Location = new Point(190, 112), Size = new Size(110, 36) };
        f.Controls.AddRange([lbl, cbo, ok, cancelar]);
        f.AcceptButton = ok;
        f.CancelButton = cancelar;
        return f.ShowDialog(this) == DialogResult.OK ? usuarios[cbo.SelectedIndex].Usuario : null;
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
}

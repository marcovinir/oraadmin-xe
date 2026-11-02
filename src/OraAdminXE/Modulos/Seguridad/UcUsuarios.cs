using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Pestaña Usuarios de Seguridad (issue #8): listar, crear, bloquear/desbloquear, expirar,
/// cambiar contraseña o cuota y eliminar. Los usuarios de Oracle y el de la sesión quedan protegidos.
/// </summary>
public sealed class UcUsuarios : UserControl
{
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _chkOracle = new() { Text = "Mostrar usuarios de Oracle", AutoSize = true, Dock = DockStyle.Right };
    private readonly Label _lblPie = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _btnCrear, _btnClave, _btnCuota, _btnBloquear, _btnDesbloquear, _btnExpirar, _btnEliminar, _btnRefrescar;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcUsuarios()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;

        _btnCrear = Tema.Boton("Crear usuario", primario: true, icono: "");
        _btnClave = Tema.Boton("Contraseña…", icono: "");
        _btnCuota = Tema.Boton("Cuota…", icono: "");
        _btnBloquear = Tema.Boton("Bloquear", icono: "");
        _btnDesbloquear = Tema.Boton("Desbloquear", icono: "");
        _btnExpirar = Tema.Boton("Expirar clave", icono: "");
        _btnEliminar = Tema.Boton("Eliminar", icono: "");
        ((BotonPlano)_btnEliminar).Estilo = EstiloBoton.Peligro;
        _btnRefrescar = Tema.Boton("Refrescar", icono: "");

        var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        izquierda.Controls.AddRange([_btnCrear, _btnClave, _btnCuota, _btnBloquear, _btnDesbloquear, _btnExpirar, _btnEliminar]);
        var derecha = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        derecha.Controls.AddRange([_btnRefrescar]);
        var barra = new Panel { Dock = DockStyle.Top, Height = 48 };
        barra.Controls.Add(izquierda);
        barra.Controls.Add(derecha);

        Tema.AplicarGrilla(_grilla);
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grilla.MultiSelect = false;
        _grilla.RowTemplate.Height = 36;
        Tema.ColumnaEstado(_grilla, "ACCOUNT_STATUS");
        Tema.MensajeVacio(_grilla, "No hay usuarios propios todavía. Cree el primero con «Crear usuario».", "");
        _grilla.SelectionChanged += (_, _) => ActualizarBotones();
        _grilla.CellFormatting += Grilla_CellFormatting;

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 32 };
        pie.Controls.Add(_lblPie);
        pie.Controls.Add(_chkOracle);

        Controls.Add(_grilla);
        Controls.Add(pie);
        Controls.Add(barra);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _chkOracle.CheckedChanged += async (_, _) => await CargarAsync();
        _btnCrear.Click += async (_, _) => await CrearAsync();
        _btnClave.Click += async (_, _) => await CambiarClaveAsync();
        _btnCuota.Click += async (_, _) => await CambiarCuotaAsync();
        _btnBloquear.Click += async (_, _) => await AccionAsync("Bloquear usuario", DdlUsuarios.Bloquear);
        _btnDesbloquear.Click += async (_, _) => await AccionAsync("Desbloquear usuario", DdlUsuarios.Desbloquear);
        _btnExpirar.Click += async (_, _) => await AccionAsync("Expirar contraseña", DdlUsuarios.Expirar);
        _btnEliminar.Click += async (_, _) => await AccionAsync("Eliminar usuario", DdlUsuarios.Eliminar, destructiva: true);
        ActualizarBotones();
    }

    private DataRow? Seleccionado =>
        _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;
    private string? UsuarioSeleccionado => Seleccionado?["USERNAME"] as string;

    public async Task CargarAsync(string? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        UseWaitCursor = true;
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlSeguridad.Usuarios, Parametro.De("todos", _chkOracle.Checked ? 1 : 0));
            _grilla.DataSource = tabla;
            Encabezados();
            if (seleccionar is not null)
                foreach (DataGridViewRow fila in _grilla.Rows)
                    if ((string)fila.Cells["USERNAME"].Value == seleccionar)
                    {
                        fila.Selected = true;
                        _grilla.CurrentCell = fila.Cells["USERNAME"];
                    }
            var propios = tabla.Select("ORACLE_MAINTAINED = 'N'").Length;
            _lblPie.Text = $"{propios} usuarios propios" + (_chkOracle.Checked ? $" · {tabla.Rows.Count - propios} de Oracle (solo lectura)" : "")
                         + $" · Los usuarios de Oracle (en gris), SYS, SYSTEM y {Bd.Usuario} (la sesión actual) son de solo lectura.";
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            UseWaitCursor = false;
            ActualizarBotones();
        }
    }

    private void Encabezados()
    {
        var nombres = new Dictionary<string, (string Texto, float Peso)>
        {
            ["USERNAME"] = ("Usuario", 16), ["ACCOUNT_STATUS"] = ("Estado", 14), ["DEFAULT_TABLESPACE"] = ("Tablespace", 12),
            ["PROFILE"] = ("Perfil", 12), ["CUOTA_MB"] = ("Cuota (MB)", 10), ["CREATED"] = ("Creado", 13),
            ["LOCK_DATE"] = ("Bloqueado", 13), ["EXPIRY_DATE"] = ("Expira", 13)
        };
        foreach (DataGridViewColumn col in _grilla.Columns)
        {
            if (nombres.TryGetValue(col.Name, out var n)) { col.HeaderText = n.Texto; col.FillWeight = n.Peso; }
            else col.Visible = false;
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
        }
    }

    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView v || e.CellStyle is null) return;
        if (EsProtegido(v.Row))
            e.CellStyle.ForeColor = Tema.TextoSecundario;
    }

    private static bool EsProtegido(DataRow fila) =>
        DdlUsuarios.EsProtegido((string)fila["USERNAME"], (string)fila["ORACLE_MAINTAINED"], Bd.Usuario);

    private void ActualizarBotones()
    {
        var fila = Seleccionado;
        var editable = fila is not null && !EsProtegido(fila);
        var estado = fila?["ACCOUNT_STATUS"] as string ?? "";
        _btnClave.Enabled = _btnCuota.Enabled = _btnExpirar.Enabled = _btnEliminar.Enabled = editable;
        _btnBloquear.Enabled = editable && !estado.Contains("LOCKED");
        _btnDesbloquear.Enabled = editable && estado.Contains("LOCKED");
        _btnCrear.Enabled = !string.IsNullOrEmpty(Bd.Usuario);
    }

    private async Task<(string[] Tablespaces, string[] Perfiles)> ListasAsync()
    {
        var ts = Bd.ConsultarAsync(SqlSeguridad.TablespacesPermanentes);
        var pf = Bd.ConsultarAsync(SqlSeguridad.Perfiles);
        await Task.WhenAll(ts, pf);
        return (ts.Result.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToArray(),
                pf.Result.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToArray());
    }

    private async Task CrearAsync()
    {
        try
        {
            var (ts, perfiles) = await ListasAsync();
            using var dlg = new DlgUsuario(DlgUsuario.Modo.Crear, null, ts, perfiles);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var sql = DdlUsuarios.Crear(dlg.Usuario, dlg.Clave, dlg.Tablespace, dlg.CuotaMb, dlg.Perfil, dlg.OtorgarSesion);
            await EjecutarAsync("Crear usuario", sql, false, $"Usuario {dlg.Usuario.ToUpperInvariant()} creado.", dlg.Usuario.ToUpperInvariant());
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task CambiarClaveAsync()
    {
        if (UsuarioSeleccionado is not { } usuario) return;
        try
        {
            using var dlg = new DlgUsuario(DlgUsuario.Modo.Clave, usuario, [], []);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await EjecutarAsync("Cambiar contraseña", [DdlUsuarios.CambiarClave(usuario, dlg.Clave)], false, $"Contraseña de {usuario} cambiada.", usuario);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task CambiarCuotaAsync()
    {
        if (Seleccionado is not { } fila) return;
        var usuario = (string)fila["USERNAME"];
        try
        {
            var (ts, _) = await ListasAsync();
            using var dlg = new DlgUsuario(DlgUsuario.Modo.Tablespace, usuario, ts, [], fila["DEFAULT_TABLESPACE"] as string);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await EjecutarAsync("Cambiar tablespace y cuota", [DdlUsuarios.CambiarTablespace(usuario, dlg.Tablespace, dlg.CuotaMb)], false,
                $"Tablespace y cuota de {usuario} actualizados.", usuario);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task AccionAsync(string titulo, Func<string, string> ddl, bool destructiva = false)
    {
        if (UsuarioSeleccionado is not { } usuario) return;
        try
        {
            await EjecutarAsync(titulo, [ddl(usuario)], destructiva, $"{titulo}: {usuario} listo.", destructiva ? null : usuario);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    /// <summary>Muestra el SQL, pide confirmación, lo ejecuta (queda en la bitácora) y recarga la lista.</summary>
    private async Task EjecutarAsync(string titulo, IReadOnlyList<string> sentencias, bool destructiva, string exito, string? seleccionar)
    {
        if (!Dialogos.ConfirmarSql(this, titulo, string.Join(";\r\n", sentencias) + ";", destructiva)) return;
        try
        {
            foreach (var sql in sentencias)
                await Bd.EjecutarAsync(Modulo.Seguridad, titulo, sql);
            Dialogos.Exito(this, exito);
        }
        finally
        {
            await CargarAsync(seleccionar);
        }
    }
}

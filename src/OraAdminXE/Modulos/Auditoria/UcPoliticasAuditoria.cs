using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Administración conservadora de políticas unificadas creadas durante esta instancia de la aplicación.</summary>
public sealed class UcPoliticasAuditoria : UserControl
{
    private readonly DataGridView _grdPoliticas = new() { Dock = DockStyle.Fill, MultiSelect = false };
    private readonly DataGridView _grdDetalle = new() { Dock = DockStyle.Fill, MultiSelect = false };
    private readonly BotonPlano _btnCrear = Tema.Boton("Crear política", true, "");
    private readonly BotonPlano _btnActivar = Tema.Boton("Activar", icono: "");
    private readonly BotonPlano _btnDesactivar = Tema.Boton("Desactivar", icono: "");
    private readonly BotonPlano _btnEliminar = Tema.Boton("Eliminar", icono: "");
    private readonly BotonPlano _btnLogon = Tema.Boton("Activar fallos de login", icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");
    private readonly Label _lblPie = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly HashSet<string> _creadasEnInstancia = new(StringComparer.OrdinalIgnoreCase);
    private bool _ocupado;
    private int _solicitudDetalle;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcPoliticasAuditoria()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;

        ((BotonPlano)_btnEliminar).Estilo = EstiloBoton.Peligro;
        foreach (var boton in new[] { _btnCrear, _btnActivar, _btnDesactivar, _btnEliminar, _btnLogon, _btnRefrescar })
            boton.Margin = new Padding(0, 4, 8, 4);

        var acciones = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        acciones.Controls.AddRange([_btnCrear, _btnActivar, _btnDesactivar, _btnEliminar, _btnLogon]);
        var refrescar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        refrescar.Controls.Add(_btnRefrescar);
        var barra = new Panel { Dock = DockStyle.Top, Height = 48 };
        barra.Controls.Add(acciones);
        barra.Controls.Add(refrescar);

        ConfigurarGrilla(_grdPoliticas, "No hay políticas para mostrar.", "");
        ConfigurarGrilla(_grdDetalle, "Seleccione una política para ver sus acciones.", "");
        _grdPoliticas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grdDetalle.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grdPoliticas.ScrollBars = _grdDetalle.ScrollBars = ScrollBars.Both;
        _grdPoliticas.ShowCellToolTips = _grdDetalle.ShowCellToolTips = true;
        Tema.ColumnaEstado(_grdPoliticas, "ACTIVA");
        Tema.ColumnaEstado(_grdPoliticas, "ORACLE_SUPPLIED", texto => texto == "YES" ? TipoEstado.Info : TipoEstado.Neutro);
        Tema.ColumnaEstado(_grdPoliticas, "COMMON", texto => texto == "YES" ? TipoEstado.Advertencia : TipoEstado.Neutro);

        var tituloDetalle = new Label { Dock = DockStyle.Top, Height = 34, Text = "Acciones y objetos de la política seleccionada", Font = Tema.FuenteNegrita, TextAlign = ContentAlignment.MiddleLeft };
        var detalle = new Panel { Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(0, 8, 0, 0) };
        detalle.Controls.Add(_grdDetalle);
        detalle.Controls.Add(tituloDetalle);
        var pie = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        pie.Controls.Add(_lblPie);

        Controls.Add(_grdPoliticas);
        Controls.Add(detalle);
        Controls.Add(pie);
        Controls.Add(barra);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnCrear.Click += async (_, _) => await CrearAsync();
        _btnActivar.Click += async (_, _) => await ActivarAsync();
        _btnDesactivar.Click += async (_, _) => await DesactivarAsync();
        _btnEliminar.Click += async (_, _) => await EliminarAsync();
        _btnLogon.Click += async (_, _) => await ActivarFallosLogonAsync();
        _grdPoliticas.SelectionChanged += async (_, _) => await CargarDetalleSeleccionadoAsync();
        _grdPoliticas.CellToolTipTextNeeded += GrdPoliticas_CellToolTipTextNeeded;
        _grdDetalle.CellToolTipTextNeeded += GrdDetalle_CellToolTipTextNeeded;
        ActualizarBotones();
    }

    public async Task CargarAsync(string? seleccionar = null)
    {
        if (_ocupado || string.IsNullOrEmpty(Bd.Usuario) || !PuedeActualizarUi) return;
        _ocupado = true;
        SetBusy(true);
        Tema.Cargando(_grdPoliticas, true);
        try
        {
            await ValidarEntornoAsync();
            var tabla = await Bd.ConsultarAsync(SqlAuditoria.Politicas);
            if (!PuedeActualizarUi) return;
            _grdPoliticas.DataSource = tabla;
            ConfigurarColumnasPoliticas();
            RestaurarSeleccion(tabla, seleccionar);
            _lblPie.Text = $"{tabla.Rows.Count} políticas. Solo se pueden modificar o eliminar políticas POL_ creadas durante esta instancia.";
        }
        catch (Exception ex) when (PuedeActualizarUi)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            _ocupado = false;
            if (PuedeActualizarUi)
            {
                Tema.Cargando(_grdPoliticas, false);
                SetBusy(false);
                await CargarDetalleSeleccionadoAsync();
            }
        }
    }

    private async Task CrearAsync()
    {
        using var dlg = new DlgCrearPoliticaAuditoria();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var politica = Identificador.Validar(dlg.Politica, "política");
            await ValidarEntornoAsync();
            if (await ExisteAsync(SqlAuditoria.ExistePolitica, "politica", politica))
                throw new ArgumentException($"La política {politica} ya existe.");
            if (dlg.EsObjeto)
            {
                var esquema = Identificador.Validar(dlg.Esquema, "esquema");
                var tabla = Identificador.Validar(dlg.Tabla, "tabla");
                if (!await ExisteTablaAsync(esquema, tabla))
                    throw new ArgumentException($"No existe la tabla {esquema}.{tabla} en DBA_TABLES.");
            }
            var sql = dlg.EsObjeto
                ? DdlPoliticasAuditoria.CrearObjeto(politica, dlg.Esquema, dlg.Tabla, dlg.Acciones)
                : DdlPoliticasAuditoria.CrearSistema(politica, dlg.Acciones);
            if (!Dialogos.ConfirmarSql(this, "Crear política", sql + ";")) return;

            await EjecutarOperacionAsync(async () =>
            {
                await Bd.EjecutarAsync(Modulo.Auditoria, "Crear política de auditoría", sql);
                _creadasEnInstancia.Add(politica);
                Dialogos.Exito(this, $"Política {politica} creada. Actívela cuando corresponda.");
            }, politica);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task ActivarAsync()
    {
        if (!TryPoliticaGestionable(out var politica)) return;
        using var dlg = new DlgActivarPoliticaAuditoria(politica);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            await ValidarPoliticaGestionableAsync(politica);
            var alcancesActuales = await AlcancesActualesAsync(politica);
            if (dlg.Usuario is { } usuario)
            {
                var u = Identificador.Validar(usuario, "usuario");
                if (!await ExisteAsync(SqlAuditoria.ExisteUsuario, "usuario", u))
                    throw new ArgumentException($"El usuario {u} no existe.");
            }
            else if (alcancesActuales.Any(DdlPoliticasAuditoria.EsGlobalOExcept))
            {
                throw new InvalidOperationException("La política ya tiene un alcance global o EXCEPT; no se emitirá AUDIT POLICY redundante.");
            }
            var sql = DdlPoliticasAuditoria.Activar(politica, dlg.Usuario, dlg.Filtro);
            if (!Dialogos.ConfirmarSql(this, "Activar política", sql + ";")) return;
            await EjecutarOperacionAsync(async () =>
            {
                await Bd.EjecutarAsync(Modulo.Auditoria, "Activar política de auditoría", sql);
                Dialogos.Exito(this, $"Política {politica} activada.");
            }, politica);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task DesactivarAsync()
    {
        if (!TryPoliticaGestionable(out var politica)) return;
        try
        {
            await ValidarPoliticaGestionableAsync(politica);
            var sentencias = DdlPoliticasAuditoria.DesactivarTodas(politica, await AlcancesActualesAsync(politica));
            if (sentencias.Count == 0)
            {
                Dialogos.Exito(this, $"La política {politica} ya está desactivada.");
                return;
            }
            if (!Dialogos.ConfirmarSql(this, "Desactivar política", MostrarSentencias(sentencias))) return;
            await EjecutarOperacionAsync(async () =>
            {
                foreach (var sql in sentencias)
                    await Bd.EjecutarAsync(Modulo.Auditoria, "Desactivar política de auditoría", sql);
                Dialogos.Exito(this, $"Política {politica} desactivada.");
            }, politica);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EliminarAsync()
    {
        if (!TryPoliticaGestionable(out var politica)) return;
        using var dlg = new DlgConfirmarEliminacionPolitica(politica);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!dlg.Confirma(politica))
        {
            Dialogos.Error(this, new ArgumentException("El nombre escrito no coincide exactamente con la política seleccionada."));
            return;
        }
        try
        {
            await ValidarPoliticaGestionableAsync(politica);
            var desactivar = DdlPoliticasAuditoria.DesactivarTodas(politica, await AlcancesActualesAsync(politica));
            var sentencias = desactivar.Append(DdlPoliticasAuditoria.Eliminar(politica)).ToArray();
            if (!Dialogos.ConfirmarSql(this, "Eliminar política", MostrarSentencias(sentencias), destructiva: true)) return;
            await EjecutarOperacionAsync(async () =>
            {
                foreach (var sql in desactivar)
                    await Bd.EjecutarAsync(Modulo.Auditoria, "Desactivar política antes de eliminar", sql);
                if ((await AlcancesActualesAsync(politica)).Count != 0)
                    throw new InvalidOperationException("La política conserva alcances habilitados; no se ejecutará DROP AUDIT POLICY.");
                await Bd.EjecutarAsync(Modulo.Auditoria, "Eliminar política de auditoría", DdlPoliticasAuditoria.Eliminar(politica));
                _creadasEnInstancia.Remove(politica);
                Dialogos.Exito(this, $"Política {politica} eliminada.");
            }, null);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task ActivarFallosLogonAsync()
    {
        try
        {
            await ValidarEntornoAsync();
            var tabla = await Bd.ConsultarAsync(SqlAuditoria.OraLogonFailures);
            if (tabla.Rows.Count != 1 || !string.Equals(tabla.Rows[0]["ORACLE_SUPPLIED"] as string, "YES", StringComparison.Ordinal) ||
                Convert.ToInt32(tabla.Rows[0]["INCLUYE_LOGON"]) != 1)
                throw new InvalidOperationException("ORA_LOGON_FAILURES no existe o no es la política predefinida de LOGON esperada.");
            var habilitados = Convert.ToInt32(tabla.Rows[0]["ALCANCES_HABILITADOS"]);
            if (habilitados > 0)
            {
                Dialogos.Exito(this, "ORA_LOGON_FAILURES ya tiene habilitaciones; por seguridad la aplicación no ampliará su alcance.");
                return;
            }
            var sql = DdlPoliticasAuditoria.ActivarFallosLogon();
            if (!Dialogos.ConfirmarSql(this, "Activar auditoría de fallos de login", sql + ";")) return;
            await EjecutarOperacionAsync(async () =>
            {
                await Bd.EjecutarAsync(Modulo.Auditoria, "Activar ORA_LOGON_FAILURES", sql);
                Dialogos.Exito(this, "Auditoría de logins fallidos activada.");
            }, "ORA_LOGON_FAILURES");
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task CargarDetalleSeleccionadoAsync()
    {
        if (_ocupado || PoliticaSeleccionada() is not { } politica || !PuedeActualizarUi) return;
        var solicitud = ++_solicitudDetalle;
        Tema.Cargando(_grdDetalle, true);
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlAuditoria.Detalle, Parametro.De("politica", politica));
            if (!PuedeActualizarUi || solicitud != _solicitudDetalle) return;
            _grdDetalle.DataSource = tabla;
            ConfigurarColumnasDetalle();
        }
        catch (Exception ex) when (PuedeActualizarUi && solicitud == _solicitudDetalle)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            if (PuedeActualizarUi && solicitud == _solicitudDetalle) Tema.Cargando(_grdDetalle, false);
            ActualizarBotones();
        }
    }

    private async Task EjecutarOperacionAsync(Func<Task> operacion, string? seleccionar)
    {
        _ocupado = true;
        SetBusy(true);
        try { await operacion(); }
        finally
        {
            _ocupado = false;
            if (PuedeActualizarUi)
            {
                SetBusy(false);
                await CargarAsync(seleccionar);
            }
        }
    }

    private async Task ValidarEntornoAsync()
    {
        var tabla = await Bd.ConsultarAsync(SqlAuditoria.Diagnostico);
        if (tabla.Rows.Count != 1 || !string.Equals(tabla.Rows[0]["CONTENEDOR"] as string, "XEPDB1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Auditoría solo opera en la PDB XEPDB1.");
        if (!string.Equals(tabla.Rows[0]["AUDIT_ADMIN"] as string, "SI", StringComparison.Ordinal) &&
            !string.Equals(tabla.Rows[0]["AUDIT_SYSTEM"] as string, "SI", StringComparison.Ordinal))
            throw new InvalidOperationException("El usuario actual necesita AUDIT_ADMIN o el privilegio AUDIT SYSTEM.");
    }

    private async Task ValidarPoliticaGestionableAsync(string politica)
    {
        await ValidarEntornoAsync();
        var tabla = await Bd.ConsultarAsync(SqlAuditoria.Politica, Parametro.De("politica", politica));
        if (tabla.Rows.Count != 1 || !EsGestionable(tabla.Rows[0], politica))
            throw new InvalidOperationException("La política cambió, no es local o no fue creada durante esta instancia. La operación se canceló.");
    }

    private async Task<bool> ExisteTablaAsync(string esquema, string tabla) =>
        Convert.ToInt32(await Bd.EscalarAsync(SqlAuditoria.ExisteTabla, Parametro.De("esquema", esquema), Parametro.De("tabla", tabla))) > 0;

    private async Task<bool> ExisteAsync(string sql, string parametro, string valor) =>
        Convert.ToInt32(await Bd.EscalarAsync(sql, Parametro.De(parametro, valor))) > 0;

    private async Task<IReadOnlyList<AlcancePoliticaAuditoria>> AlcancesActualesAsync(string politica)
    {
        var tabla = await Bd.ConsultarAsync(SqlAuditoria.Alcances, Parametro.De("politica", politica));
        return tabla.AsEnumerable().Select(r => new AlcancePoliticaAuditoria(
            r.Field<string>("ENABLED_OPTION") ?? "", r.Field<string>("ENTITY_NAME") ?? "",
            r.Field<string>("ENTITY_TYPE") ?? "", r.Field<string>("SUCCESS") ?? "", r.Field<string>("FAILURE") ?? "")).ToArray();
    }

    private bool TryPoliticaGestionable(out string politica)
    {
        politica = PoliticaSeleccionada() ?? "";
        if (FilaSeleccionada is not { } fila || !EsGestionable(fila, politica))
        {
            Dialogos.Error(this, new InvalidOperationException("Seleccione una política POL_ local creada durante esta instancia."));
            return false;
        }
        return true;
    }

    private bool EsGestionable(DataRow fila, string politica) => DdlPoliticasAuditoria.PuedeEliminar(politica,
        fila["ORACLE_SUPPLIED"] as string, fila["COMMON"] as string, fila["INHERITED"] as string, _creadasEnInstancia.Contains(politica));

    private DataRow? FilaSeleccionada => _grdPoliticas.SelectedRows.Count > 0 && _grdPoliticas.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;
    private string? PoliticaSeleccionada() => FilaSeleccionada?["POLICY_NAME"] as string;
    private bool PuedeActualizarUi => !IsDisposed && !Disposing;

    private void ActualizarBotones()
    {
        var gestionable = !_ocupado && FilaSeleccionada is { } fila && PoliticaSeleccionada() is { } p && EsGestionable(fila, p);
        _btnCrear.Enabled = !_ocupado && !string.IsNullOrEmpty(Bd.Usuario);
        _btnRefrescar.Enabled = !_ocupado && !string.IsNullOrEmpty(Bd.Usuario);
        _btnLogon.Enabled = !_ocupado && !string.IsNullOrEmpty(Bd.Usuario);
        _btnActivar.Enabled = _btnDesactivar.Enabled = _btnEliminar.Enabled = gestionable;
    }

    private void SetBusy(bool ocupado)
    {
        UseWaitCursor = ocupado;
        ActualizarBotones();
    }

    private static string MostrarSentencias(IEnumerable<string> sentencias) => string.Join(";\r\n", sentencias) + ";";

    private static void ConfigurarGrilla(DataGridView grilla, string vacio, string icono)
    {
        Tema.AplicarGrilla(grilla);
        grilla.RowTemplate.Height = 34;
        Tema.MensajeVacio(grilla, vacio, icono);
    }

    private void ConfigurarColumnasPoliticas()
    {
        var columnas = new Dictionary<string, (string Texto, int Ancho, int Minimo)>
        {
            ["POLICY_NAME"] = ("Política", 290, 240), ["ACTIVA"] = ("Activa", 90, 80), ["ALCANCE"] = ("Alcance y entidades", 300, 220),
            ["CONFIGURACION"] = ("Éxito / fallo", 240, 190), ["CANTIDAD_ACCIONES"] = ("Acciones", 95, 85),
            ["ORACLE_SUPPLIED"] = ("Oracle", 95, 85), ["COMMON"] = ("Común", 90, 80), ["INHERITED"] = ("Heredada", 100, 90)
        };
        foreach (DataGridViewColumn c in _grdPoliticas.Columns)
            if (columnas.TryGetValue(c.Name, out var formato))
            {
                c.HeaderText = formato.Texto;
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                c.MinimumWidth = formato.Minimo;
                c.Width = formato.Ancho;
            }
            else c.Visible = false;
    }

    private void ConfigurarColumnasDetalle()
    {
        var columnas = new Dictionary<string, (string Texto, int Ancho, int Minimo)>
        {
            ["AUDIT_OPTION"] = ("Acción", 180, 140), ["AUDIT_OPTION_TYPE"] = ("Tipo", 185, 150), ["OBJECT_SCHEMA"] = ("Esquema", 160, 130),
            ["OBJECT_NAME"] = ("Objeto", 230, 180), ["OBJECT_TYPE"] = ("Tipo objeto", 145, 120), ["AUDIT_ONLY_TOPLEVEL"] = ("Solo nivel superior", 135, 120)
        };
        foreach (DataGridViewColumn c in _grdDetalle.Columns)
            if (columnas.TryGetValue(c.Name, out var formato))
            {
                c.HeaderText = formato.Texto;
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                c.MinimumWidth = formato.Minimo;
                c.Width = formato.Ancho;
            }
            else c.Visible = false;
    }

    private void GrdPoliticas_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grdPoliticas.Columns[e.ColumnIndex].Name != "POLICY_NAME") return;
        if (_grdPoliticas.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string politica)
            e.ToolTipText = politica;
    }

    private void GrdDetalle_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grdDetalle.Columns[e.ColumnIndex].Name is not ("AUDIT_OPTION" or "OBJECT_SCHEMA" or "OBJECT_NAME")) return;
        if (_grdDetalle.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string valor && valor.Length > 0)
            e.ToolTipText = valor;
    }

    private void RestaurarSeleccion(DataTable tabla, string? preferida)
    {
        if (tabla.Rows.Count == 0) return;
        var politica = tabla.AsEnumerable().Select(r => r.Field<string>("POLICY_NAME")).FirstOrDefault(p => p == preferida) ??
                       tabla.Rows[0].Field<string>("POLICY_NAME");
        foreach (DataGridViewRow fila in _grdPoliticas.Rows)
            if (string.Equals(fila.Cells["POLICY_NAME"].Value as string, politica, StringComparison.Ordinal))
            {
                fila.Selected = true;
                _grdPoliticas.CurrentCell = fila.Cells["POLICY_NAME"];
                break;
            }
    }
}

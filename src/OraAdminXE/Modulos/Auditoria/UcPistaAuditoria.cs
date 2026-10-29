using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>Consulta y exportación local, exclusivamente de lectura, de UNIFIED_AUDIT_TRAIL.</summary>
public sealed class UcPistaAuditoria : UserControl
{
    private const string Todos = "Todos";
    private readonly DateTimePicker _dtpDesdeFecha = Fecha();
    private readonly DateTimePicker _dtpDesdeHora = Hora();
    private readonly DateTimePicker _dtpHastaFecha = Fecha();
    private readonly DateTimePicker _dtpHastaHora = Hora();
    private readonly ComboBox _cboUsuario = Selector(190);
    private readonly ComboBox _cboAccion = Selector(145, soloLista: true);
    private readonly ComboBox _cboPolitica = Selector(210);
    private readonly CheckBox _chkFallidos = new() { Text = "Solo fallidos", AutoSize = true, ForeColor = Tema.Texto };
    private readonly BotonPlano _btnBuscar = Tema.Boton("Buscar", primario: true, icono: "");
    private readonly BotonPlano _btnCsv = Tema.Boton("Exportar CSV", icono: "");
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill, MultiSelect = false, ScrollBars = ScrollBars.Both };
    private readonly TextBox _txtDetalle = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = Tema.FuenteCodigo
    };
    private readonly Label _lblEvento = new() { Dock = DockStyle.Top, Height = 64, Font = Tema.FuenteNegrita, TextAlign = ContentAlignment.TopLeft };
    private readonly Label _lblPie = new() { Dock = DockStyle.Bottom, Height = 30, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private bool _ocupado;
    private bool _inicializado;
    private int _solicitudDetalle;

    private static IBaseDatos Bd => Sesion.Bd;
    private bool PuedeActualizarUi => !IsDisposed && !Disposing;

    public UcPistaAuditoria()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;

        _cboUsuario.AccessibleName = "Usuario de base de datos; escriba un usuario válido o seleccione Todos";
        _cboPolitica.AccessibleName = "Política de auditoría; escriba una política válida o seleccione Todas";
        _cboAccion.Items.Add("Todas");
        _cboAccion.Items.AddRange(SqlPistaAuditoria.AccionesPermitidas.Cast<object>().ToArray());
        _cboAccion.SelectedIndex = 0;

        var filtros = CrearBarraFiltros();
        Tema.AplicarGrilla(_grilla);
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grilla.RowTemplate.Height = 34;
        _grilla.ShowCellToolTips = true;
        Tema.MensajeVacio(_grilla, "No hay eventos para estos filtros.", "");
        _grilla.CellFormatting += Grilla_CellFormatting;
        _grilla.CellToolTipTextNeeded += Grilla_CellToolTipTextNeeded;
        _grilla.SelectionChanged += async (_, _) => await CargarDetalleSeleccionadoAsync();

        var cabeceraDetalle = new Panel { Dock = DockStyle.Top, Height = 68 };
        cabeceraDetalle.Controls.Add(_lblEvento);
        var detalle = new Panel { Dock = DockStyle.Bottom, Height = 240, Padding = new Padding(0, 8, 0, 0) };
        detalle.Controls.Add(_txtDetalle);
        detalle.Controls.Add(cabeceraDetalle);

        Controls.Add(_grilla);
        Controls.Add(detalle);
        Controls.Add(_lblPie);
        Controls.Add(filtros);

        _btnBuscar.Click += async (_, _) => await BuscarAsync(actualizarListas: true);
        _btnCsv.Click += ExportarCsv;
        _cboUsuario.KeyDown += async (_, e) => await BuscarConEnterAsync(e);
        _cboPolitica.KeyDown += async (_, e) => await BuscarConEnterAsync(e);
        _btnCsv.Enabled = false;
        LimpiarDetalle("Seleccione un evento para ver sus detalles y SQL.");
    }

    public async Task CargarAsync()
    {
        if (_ocupado || string.IsNullOrEmpty(Bd.Usuario) || !PuedeActualizarUi) return;
        if (!_inicializado)
        {
            _ocupado = true;
            SetBusy(true);
            try
            {
                await ValidarPdbAsync();
                // LOCALTIMESTAMP fija el rango inicial en la hora de la base, no en la del equipo cliente.
                var ahora = await Bd.EscalarAsync(SqlPistaAuditoria.HoraBaseDatos);
                if (ahora is not DateTime horaBase)
                    throw new InvalidOperationException("Oracle no devolvió una hora de base de datos válida.");
                EstablecerRango(horaBase.AddHours(-24), horaBase);
                await CargarSelectoresAsync();
                _inicializado = true;
            }
            catch (Exception ex) when (PuedeActualizarUi)
            {
                Dialogos.Error(this, ex);
                return;
            }
            finally
            {
                _ocupado = false;
                if (PuedeActualizarUi) SetBusy(false);
            }
        }
        await BuscarAsync(actualizarListas: false);
    }

    private async Task BuscarConEnterAsync(KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        await BuscarAsync(actualizarListas: true);
    }

    private async Task BuscarAsync(bool actualizarListas)
    {
        if (_ocupado || string.IsNullOrEmpty(Bd.Usuario) || !PuedeActualizarUi) return;
        FiltroPistaAuditoria filtro;
        try { filtro = FiltroActual(); }
        catch (Exception ex) { Dialogos.Error(this, ex); return; }

        _ocupado = true;
        var solicitud = ++_solicitudDetalle; // invalida detalles de la búsqueda anterior.
        SetBusy(true);
        Tema.Cargando(_grilla, true);
        try
        {
            if (actualizarListas) await CargarSelectoresAsync(filtro.Desde, filtro.Hasta);
            var consulta = SqlPistaAuditoria.CrearConsulta(filtro);
            var tabla = await Bd.ConsultarAsync(consulta.Sql, consulta.Parametros.ToArray());
            if (!PuedeActualizarUi || solicitud != _solicitudDetalle) return;

            _grilla.DataSource = tabla;
            ConfigurarColumnas();
            _btnCsv.Enabled = tabla.Rows.Count > 0;
            var fallidos = tabla.AsEnumerable().Count(f => SqlPistaAuditoria.EsFallido(f["RETURN_CODE"]));
            _lblPie.Text = $"{tabla.Rows.Count} eventos. Se muestran como máximo {SqlPistaAuditoria.MaximoFilas} coincidencias más recientes" +
                           (tabla.Rows.Count == SqlPistaAuditoria.MaximoFilas ? "; refine los filtros si necesita otros eventos." : ".") +
                           (fallidos > 0 ? $" {fallidos} fallidos se muestran en rojo." : "");
            LimpiarDetalle("Seleccione un evento para ver sus detalles y SQL.");
        }
        catch (Exception ex) when (PuedeActualizarUi)
        {
            // No incluir SQL_TEXT ni valores de auditoría en mensajes propios; Dialogos conserva el ORA original.
            Dialogos.Error(this, ex);
        }
        finally
        {
            _ocupado = false;
            if (PuedeActualizarUi)
            {
                Tema.Cargando(_grilla, false);
                SetBusy(false);
            }
        }
    }

    private async Task CargarSelectoresAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        var inicio = desde ?? Combinar(_dtpDesdeFecha, _dtpDesdeHora);
        var fin = hasta ?? Combinar(_dtpHastaFecha, _dtpHastaHora);
        if (fin < inicio) return;

        var usuarioAnterior = _cboUsuario.Text;
        var politicaAnterior = _cboPolitica.Text;
        var usuarios = await Bd.ConsultarAsync(SqlPistaAuditoria.UsuariosRecientes, Parametro.De("desde", inicio), Parametro.De("hasta", fin));
        var politicas = await Bd.ConsultarAsync(SqlPistaAuditoria.PoliticasDisponibles);
        if (!PuedeActualizarUi) return;
        RellenarSelector(_cboUsuario, usuarios, "DBUSERNAME", usuarioAnterior);
        RellenarSelector(_cboPolitica, politicas, "POLICY_NAME", politicaAnterior);
    }

    private async Task ValidarPdbAsync()
    {
        var contexto = await Bd.ConsultarAsync(SqlPistaAuditoria.ContextoPdb);
        if (contexto.Rows.Count != 1 || !string.Equals(contexto.Rows[0]["CONTENEDOR"] as string, "XEPDB1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La pista de auditoría solo opera en la PDB XEPDB1.");
    }

    private async Task CargarDetalleSeleccionadoAsync()
    {
        if (_ocupado || !PuedeActualizarUi) return;
        var fila = FilaSeleccionada;
        var solicitud = ++_solicitudDetalle;
        if (fila is null)
        {
            LimpiarDetalle("Seleccione un evento para ver sus detalles y SQL.");
            return;
        }
        MostrarMetadatos(fila);
        if (!SqlPistaAuditoria.TryCrearIdentidad(fila, out var identidad))
        {
            _txtDetalle.Text = "No se recuperará SQL_TEXT expandido: el evento no trae una identidad completa y única para una consulta segura.";
            return;
        }

        _txtDetalle.Text = "Cargando SQL_TEXT…";
        try
        {
            var consulta = SqlPistaAuditoria.CrearConsultaDetalle(identidad);
            var detalle = await Bd.ConsultarAsync(consulta.Sql, consulta.Parametros.ToArray());
            if (!PuedeActualizarUi || solicitud != _solicitudDetalle) return;
            if (detalle.Rows.Count != 1)
            {
                _txtDetalle.Text = "No se recuperará SQL_TEXT: la identidad no devolvió exactamente un evento.";
                return;
            }
            var texto = detalle.Rows[0]["SQL_TEXT"] as string;
            if (string.IsNullOrEmpty(texto))
            {
                _txtDetalle.Text = "No disponible para este evento.";
                return;
            }
            var truncado = string.Equals(detalle.Rows[0]["TRUNCADO"] as string, "SI", StringComparison.Ordinal);
            _txtDetalle.Text = texto.Replace("\r\n", "\n").Replace("\n", "\r\n") +
                               (truncado ? $"\r\n\r\n[Vista limitada a {SqlPistaAuditoria.MaximoDetalleSql:N0} caracteres por seguridad.]" : "");
        }
        catch (Exception ex) when (PuedeActualizarUi && solicitud == _solicitudDetalle)
        {
            _txtDetalle.Text = "No fue posible cargar SQL_TEXT para este evento.";
            Dialogos.Error(this, ex);
        }
    }

    private void ExportarCsv(object? sender, EventArgs e)
    {
        if (_grilla.DataSource is not DataTable tabla || tabla.Rows.Count == 0) return;
        // La exportación es local; no se registra ni se envía SQL_TEXT ampliado a la base de datos.
        Dialogos.ExportarCsv(this, ExportacionPistaAuditoria.Preparar(tabla), "pista_auditoria");
    }

    private FlowLayoutPanel CrearBarraFiltros()
    {
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 96, WrapContents = true, AutoScroll = true, Padding = new Padding(0, 0, 0, 6)
        };
        barra.Controls.AddRange(
        [
            Grupo("Desde", _dtpDesdeFecha, _dtpDesdeHora), Grupo("Hasta", _dtpHastaFecha, _dtpHastaHora),
            Grupo("Usuario BD", _cboUsuario), Grupo("Acción", _cboAccion), Grupo("Política", _cboPolitica),
            GrupoAcciones()
        ]);
        return barra;
    }

    private FlowLayoutPanel Grupo(string etiqueta, params Control[] controles)
    {
        var grupo = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
            Margin = new Padding(0, 4, 14, 4), Padding = Padding.Empty
        };
        var titulo = Etiqueta(etiqueta);
        titulo.Margin = new Padding(0, 7, 6, 0);
        grupo.Controls.Add(titulo);
        foreach (var control in controles)
        {
            control.Margin = new Padding(0, 0, 6, 0);
            grupo.Controls.Add(control);
        }
        return grupo;
    }

    private FlowLayoutPanel GrupoAcciones()
    {
        var grupo = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
            Margin = new Padding(0, 4, 0, 4), Padding = Padding.Empty
        };
        _chkFallidos.Margin = new Padding(0, 7, 12, 0);
        _btnBuscar.Margin = new Padding(0, 0, 8, 0);
        _btnCsv.Margin = Padding.Empty;
        grupo.Controls.AddRange([_chkFallidos, _btnBuscar, _btnCsv]);
        return grupo;
    }

    private FiltroPistaAuditoria FiltroActual() => SqlPistaAuditoria.NormalizarFiltro(
        Combinar(_dtpDesdeFecha, _dtpDesdeHora), Combinar(_dtpHastaFecha, _dtpHastaHora),
        _cboUsuario.Text, _cboAccion.SelectedItem as string, _cboPolitica.Text, _chkFallidos.Checked);

    private void EstablecerRango(DateTime desde, DateTime hasta)
    {
        _dtpDesdeFecha.Value = desde.Date;
        _dtpDesdeHora.Value = DateTime.Today.Add(desde.TimeOfDay);
        _dtpHastaFecha.Value = hasta.Date;
        _dtpHastaHora.Value = DateTime.Today.Add(hasta.TimeOfDay);
    }

    private void MostrarMetadatos(DataRow fila)
    {
        var fecha = fila["EVENT_TIMESTAMP"] is DateTime f ? f.ToString("dd/MM/yyyy HH:mm:ss") : "—";
        _lblEvento.Text = $"{fecha} (hora de la BD) · Usuario BD: {Valor(fila, "DBUSERNAME")} · Usuario SO: {Valor(fila, "OS_USERNAME")} · Host: {Valor(fila, "USERHOST")}\r\n" +
                           $"Acción: {Valor(fila, "ACTION_NAME")} · Objeto: {Valor(fila, "OBJECT_SCHEMA")}.{Valor(fila, "OBJECT_NAME")} · Políticas: {Valor(fila, "UNIFIED_AUDIT_POLICIES")} · {SqlPistaAuditoria.InterpretarReturnCode(fila["RETURN_CODE"])}";
    }

    private void LimpiarDetalle(string mensaje)
    {
        _lblEvento.Text = "Detalle del evento";
        _txtDetalle.Text = mensaje;
    }

    private void ConfigurarColumnas()
    {
        var columnas = new Dictionary<string, (string Texto, int Ancho)>
        {
            ["EVENT_TIMESTAMP"] = ("Fecha (BD)", 155), ["DBUSERNAME"] = ("Usuario BD", 130), ["OS_USERNAME"] = ("Usuario SO", 130),
            ["USERHOST"] = ("Host", 170), ["ACTION_NAME"] = ("Acción", 130), ["OBJECT_SCHEMA"] = ("Esquema", 125),
            ["OBJECT_NAME"] = ("Objeto", 180), ["RETURN_CODE"] = ("Código", 85), ["UNIFIED_AUDIT_POLICIES"] = ("Políticas", 240),
            ["SQL_TEXT_PREVIEW"] = ("SQL (vista previa)", 360)
        };
        foreach (DataGridViewColumn columna in _grilla.Columns)
        {
            if (!columnas.TryGetValue(columna.Name, out var formato)) { columna.Visible = false; continue; }
            columna.HeaderText = formato.Texto;
            columna.Width = formato.Ancho;
            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            if (columna.ValueType == typeof(DateTime)) columna.DefaultCellStyle.Format = "dd/MM/yy HH:mm:ss";
        }
    }

    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView vista) return;
        if (SqlPistaAuditoria.EsFallido(vista["RETURN_CODE"]))
        {
            e.CellStyle.BackColor = Tema.PeligroFondo;
            e.CellStyle.ForeColor = Tema.Peligro;
        }
    }

    private void Grilla_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView vista) return;
        var nombre = _grilla.Columns[e.ColumnIndex].Name;
        if (nombre is "SQL_TEXT_PREVIEW" or "UNIFIED_AUDIT_POLICIES" or "USERHOST" or "OBJECT_NAME")
            e.ToolTipText = Valor(vista.Row, nombre);
    }

    private void SetBusy(bool ocupado)
    {
        UseWaitCursor = ocupado;
        _btnBuscar.Enabled = !ocupado && !string.IsNullOrEmpty(Bd.Usuario);
        _btnCsv.Enabled = !ocupado && _grilla.DataSource is DataTable tabla && tabla.Rows.Count > 0;
    }

    private DataRow? FilaSeleccionada => _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView vista ? vista.Row : null;
    private static string Valor(DataRow fila, string columna) => fila.IsNull(columna) ? "—" : Convert.ToString(fila[columna]) ?? "—";
    private static Label Etiqueta(string texto) => new() { Text = texto, AutoSize = true, ForeColor = Tema.TextoSecundario };
    private static DateTimePicker Fecha() => new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private static DateTimePicker Hora() => new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 92 };
    private static ComboBox Selector(int ancho, bool soloLista = false) => new() { Width = ancho, DropDownStyle = soloLista ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown };
    private static DateTime Combinar(DateTimePicker fecha, DateTimePicker hora) => fecha.Value.Date.Add(hora.Value.TimeOfDay);

    private static void RellenarSelector(ComboBox selector, DataTable tabla, string columna, string anterior)
    {
        selector.BeginUpdate();
        try
        {
            selector.Items.Clear();
            selector.Items.Add(Todos);
            foreach (DataRow fila in tabla.Rows)
                if (fila[columna] is string texto && texto.Length > 0) selector.Items.Add(texto);
            selector.Text = string.IsNullOrWhiteSpace(anterior) ? Todos : anterior;
        }
        finally { selector.EndUpdate(); }
    }
}

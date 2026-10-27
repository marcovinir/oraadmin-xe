using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Respaldos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>
/// Pestaña «Restaurar» de Recuperación (issue #13): elegir un respaldo COMPLETADO del catálogo, qué restaurar
/// (todo, algunos esquemas o algunas tablas), qué hacer si la tabla existe y si se remapea a otro esquema.
/// </summary>
public sealed class UcRestaurar : UserControl
{
    private sealed record OpcionAccion(AccionExistente Accion, string Texto, string Detalle)
    {
        public override string ToString() => Texto;
    }

    private static readonly OpcionAccion[] Acciones =
    [
        new(AccionExistente.Skip, "SKIP — Omitir la tabla", "Si la tabla ya existe no se toca (ni estructura ni datos)."),
        new(AccionExistente.Append, "APPEND — Agregar filas", "Inserta las filas del respaldo en la tabla existente (puede duplicar o chocar con la llave primaria)."),
        new(AccionExistente.Truncate, "TRUNCATE — Vaciar y cargar", "⚠ Borra todas las filas actuales y carga las del respaldo."),
        new(AccionExistente.Replace, "REPLACE — Borrar y recrear", "⚠ Elimina la tabla actual y la crea de nuevo tal como estaba en el respaldo.")
    ];

    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly RadioButton _rdoTodo = new() { Text = "Todo el respaldo", AutoSize = true, Margin = new Padding(2, 2, 18, 4) };
    private readonly RadioButton _rdoEsquemas = new() { Text = "Algunos esquemas", AutoSize = true, Margin = new Padding(2, 2, 18, 4) };
    private readonly RadioButton _rdoTablas = new() { Text = "Algunas tablas", AutoSize = true, Margin = new Padding(2, 2, 0, 4) };
    private readonly CheckedListBox _clbEsquemas = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly ComboBox _cboEsquema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckedListBox _clbTablas = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None, MultiColumn = true, ColumnWidth = 200 };
    private readonly TextBox _txtOtraTabla = new() { CharacterCasing = CharacterCasing.Upper, Dock = DockStyle.Fill, PlaceholderText = "Otra tabla…" };
    private readonly Label _lblTodo = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, Padding = new Padding(4, 6, 4, 0) };
    private readonly Panel _pnlEsquemas = new() { Dock = DockStyle.Fill };
    private readonly Panel _pnlTablas = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cboAccion = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly Label _lblAccion = new() { Dock = DockStyle.Top, Height = 44, ForeColor = Tema.TextoSecundario, Padding = new Padding(0, 6, 0, 0) };
    private readonly CheckBox _chkRemap = new() { Text = "Restaurar en otro esquema (REMAP_SCHEMA)", AutoSize = true, Dock = DockStyle.Top };
    private readonly TextBox _txtDestino = new() { CharacterCasing = CharacterCasing.Upper, Dock = DockStyle.Top, PlaceholderText = "Esquema destino, por ejemplo DEMO_COPIA", Enabled = false };
    private readonly Label _lblRemap = new()
    {
        Dock = DockStyle.Top, Height = 40, ForeColor = Tema.TextoSecundario, Padding = new Padding(0, 6, 0, 0),
        Text = "Si el usuario no existe, Data Pump lo crea (al restaurar esquemas)."
    };
    private readonly Label _lblResultado = new() { Dock = DockStyle.Fill, ForeColor = Tema.Texto, Padding = new Padding(0, 8, 0, 0) };
    private readonly BarraProgreso _barra = new() { Dock = DockStyle.Top, Visible = false };
    private readonly Label _lblEstado = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnVerSql = Tema.Boton("Ver SQL", icono: "");
    private readonly BotonPlano _btnRestaurar = Tema.Boton("Restaurar", primario: true, icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");
    private HashSet<string> _esquemasOracle = [];
    private bool _ocupado;

    private static IBaseDatos Bd => Sesion.Bd;

    /// <summary>Se dispara al terminar una restauración (bien o mal), para recargar el historial (#14).</summary>
    public event EventHandler<ResultadoRestauracion?>? RestauracionTerminada;

    public UcRestaurar()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(20, 8, 20, 12);
        Font = Tema.Fuente;

        // ---- Arriba: respaldos del catálogo
        Tema.AplicarGrilla(_grilla);
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grilla.MultiSelect = false;
        _grilla.RowTemplate.Height = 34;
        _grilla.ShowCellToolTips = true;
        Tema.ColumnaEstado(_grilla, "TIPO", _ => TipoEstado.Acento);
        Tema.ColumnaEstado(_grilla, "EN_DISCO", t => t == "SÍ" ? TipoEstado.Ok : TipoEstado.Neutro);
        Tema.MensajeVacio(_grilla, "No hay respaldos completados. Haga uno en el módulo Respaldos.", "");
        _grilla.CellFormatting += Grilla_CellFormatting;
        _grilla.SelectionChanged += async (_, _) => await ElegirRespaldoAsync();
        var refrescar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        refrescar.Controls.Add(_btnRefrescar);
        var tituloRespaldos = new Panel { Dock = DockStyle.Top, Height = 46 };
        tituloRespaldos.Controls.Add(Asistente.Seccion("1", "Respaldo", "Elija de dónde restaurar (solo los COMPLETADOS cuyo .DMP sigue en disco)"));
        tituloRespaldos.Controls.Add(refrescar);
        var arriba = new Panel { Dock = DockStyle.Top, Height = 230, Padding = new Padding(0, 0, 0, 8) };
        arriba.Controls.Add(_grilla);
        arriba.Controls.Add(tituloRespaldos);

        // ---- Abajo a la izquierda: qué restaurar
        var alcance = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false };
        alcance.Controls.AddRange([_rdoTodo, _rdoEsquemas, _rdoTablas]);
        _pnlEsquemas.Controls.Add(Asistente.Marco(_clbEsquemas));
        var otraTabla = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 36, ColumnCount = 3, Padding = new Padding(0, 6, 0, 0) };
        otraTabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        otraTabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        otraTabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        otraTabla.Controls.Add(new Label { Text = "¿No está en la lista?", AutoSize = true, ForeColor = Tema.TextoSecundario, Margin = new Padding(0, 4, 12, 0) }, 0, 0);
        otraTabla.Controls.Add(_txtOtraTabla, 1, 0);
        var agregar = Asistente.Enlace("Agregar", AgregarTabla);
        agregar.Margin = new Padding(12, 4, 0, 0);
        otraTabla.Controls.Add(agregar, 2, 0);
        var esquemaTablas = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 2, Padding = new Padding(0, 0, 0, 6) };
        esquemaTablas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        esquemaTablas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        esquemaTablas.Controls.Add(new Label { Text = "Esquema", AutoSize = true, Margin = new Padding(0, 4, 12, 0) }, 0, 0);
        esquemaTablas.Controls.Add(_cboEsquema, 1, 0);
        _pnlTablas.Controls.Add(Asistente.Marco(_clbTablas));
        _pnlTablas.Controls.Add(esquemaTablas);
        _pnlTablas.Controls.Add(otraTabla);
        var objetos = new Panel { Dock = DockStyle.Fill };
        objetos.Controls.AddRange([_pnlEsquemas, _pnlTablas, _lblTodo]);
        var izquierda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 20, 0) };
        izquierda.Controls.Add(objetos);
        izquierda.Controls.Add(alcance);
        izquierda.Controls.Add(Asistente.Seccion("2", "Qué restaurar", "El modo del import no tiene que ser el del respaldo"));

        // ---- Abajo a la derecha: opciones y resultado
        _cboAccion.Items.AddRange(Acciones);
        var derecha = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 0, 0) };
        derecha.Controls.Add(_lblResultado);
        derecha.Controls.Add(_lblRemap);
        derecha.Controls.Add(_txtDestino);
        derecha.Controls.Add(Asistente.Espacio(6));
        derecha.Controls.Add(_chkRemap);
        derecha.Controls.Add(_lblAccion);
        derecha.Controls.Add(_cboAccion);
        derecha.Controls.Add(Asistente.Seccion("3", "Si la tabla ya existe", "TABLE_EXISTS_ACTION"));
        Asistente.Divisoria(derecha, arriba: false);

        var division = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        division.Controls.Add(izquierda, 0, 0);
        division.Controls.Add(derecha, 1, 0);

        // ---- Pie: progreso y acciones
        var botones = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
        botones.Controls.AddRange([_btnRestaurar, _btnVerSql]);
        var estado = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 24, 0) };
        estado.Controls.Add(_lblEstado);
        estado.Controls.Add(_barra);
        var pie = new Panel { Dock = DockStyle.Bottom, Height = 62 };
        pie.Controls.Add(estado);
        pie.Controls.Add(botones);
        Asistente.Divisoria(pie, arriba: true);

        Controls.Add(division);
        Controls.Add(arriba);
        Controls.Add(pie);

        foreach (var rdo in new[] { _rdoTodo, _rdoEsquemas, _rdoTablas })
            rdo.CheckedChanged += (_, _) => { if (rdo.Checked) CambiarAlcance(); };
        _cboEsquema.SelectedIndexChanged += async (_, _) => await CargarTablasAsync();
        _cboAccion.SelectedIndexChanged += (_, _) => _lblAccion.Text = (_cboAccion.SelectedItem as OpcionAccion)?.Detalle;
        _chkRemap.CheckedChanged += (_, _) => { _txtDestino.Enabled = _chkRemap.Checked; if (_chkRemap.Checked) _txtDestino.Focus(); };
        _txtOtraTabla.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { AgregarTabla(); e.SuppressKeyPress = true; } };
        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnVerSql.Click += (_, _) => VerSql();
        _btnRestaurar.Click += async (_, _) => await RestaurarAsync();
        _cboAccion.SelectedIndex = 0;
        _rdoTablas.Checked = true;
        ActualizarBotones();
    }

    // ------------------------------------------------------------------ datos

    private AlcanceRestauracion Alcance =>
        _rdoTodo.Checked ? AlcanceRestauracion.Todo : _rdoEsquemas.Checked ? AlcanceRestauracion.Esquemas : AlcanceRestauracion.Tablas;

    private RespaldoRestaurable? Respaldo =>
        _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView v ? Restaurable(v.Row) : null;

    /// <summary>Convierte la fila del catálogo; los objetos vienen como «ESQ, ESQ.TABLA» en la columna OBJETOS.</summary>
    private static RespaldoRestaurable Restaurable(DataRow fila)
    {
        var objetos = fila["OBJETOS"] is string texto
            ? texto.Split(", ").Select(o => o.Split('.')).Select(p => (p[0], p.Length > 1 ? p[1] : (string?)null)).ToList()
            : [];
        return new(Convert.ToInt32(fila["ID_RESPALDO"]), Enum.Parse<TipoRespaldo>((string)fila["TIPO"], ignoreCase: true),
            (string)fila["DIRECTORIO"], (string)fila["RUTA_SO"], (string)fila["ARCHIVO_DUMP"], (string)fila["ARCHIVO_LOG"], objetos);
    }

    /// <summary>Recarga los respaldos COMPLETADOS (conserva el seleccionado).</summary>
    public async Task CargarAsync(int? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario) || _ocupado) return;
        var anterior = seleccionar ?? Respaldo?.Id;
        Tema.Cargando(_grilla, true);
        try
        {
            var respaldos = Bd.ConsultarAsync(SqlRecuperacion.RespaldosCompletados);
            var deOracle = Bd.ConsultarAsync(SqlRecuperacion.EsquemasDeOracle);
            await Task.WhenAll(respaldos, deOracle);
            _esquemasOracle = deOracle.Result.AsEnumerable().Select(r => (string)r[0]).ToHashSet();

            var tabla = respaldos.Result;
            tabla.Columns.Add("EN_DISCO", typeof(string));
            foreach (DataRow r in tabla.Rows)
                r["EN_DISCO"] = File.Exists(Path.Combine((string)r["RUTA_SO"], (string)r["ARCHIVO_DUMP"])) ? "SÍ" : "NO";
            _grilla.DataSource = tabla;
            Encabezados();
            var fila = _grilla.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => Convert.ToInt32(f.Cells["ID_RESPALDO"].Value) == anterior)
                       ?? _grilla.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => (string)f.Cells["EN_DISCO"].Value == "SÍ");
            if (fila is not null)
            {
                fila.Selected = true;
                _grilla.CurrentCell = fila.Cells["TIPO"];
            }
            await ElegirRespaldoAsync();
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        finally { Tema.Cargando(_grilla, false); }
    }

    private void Encabezados()
    {
        var columnas = new Dictionary<string, (string Texto, float Peso)>
        {
            ["TIPO"] = ("Tipo", 9), ["OBJETOS"] = ("Objetos", 26), ["ARCHIVO_DUMP"] = ("Archivo", 26), ["FECHA_INICIO"] = ("Fecha", 15),
            ["TAMANO_MB"] = ("MB", 8), ["EN_DISCO"] = ("En disco", 10), ["OBSERVACION"] = ("Observación", 16)
        };
        foreach (DataGridViewColumn col in _grilla.Columns)
        {
            if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
            else col.Visible = false;
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd/MM/yy HH:mm";
        }
        _grilla.Columns["TAMANO_MB"]!.DefaultCellStyle.Format = "0.00";
        _grilla.Columns["TAMANO_MB"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        var orden = 0;
        foreach (var nombre in columnas.Keys) _grilla.Columns[nombre]!.DisplayIndex = orden++;
    }

    /// <summary>Los respaldos cuyo .DMP se borró se ven en gris y no se pueden restaurar.</summary>
    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView v) return;
        var columna = _grilla.Columns[e.ColumnIndex].Name;
        if ((string)v["EN_DISCO"] == "NO" && columna != "EN_DISCO")
        {
            e.CellStyle.ForeColor = Tema.BordeFuerte;
            e.CellStyle.SelectionForeColor = Tema.TextoSecundario;
        }
        if (columna == "OBJETOS" && v["OBJETOS"] is DBNull)
        {
            e.Value = "Toda la PDB";
            e.FormattingApplied = true;
        }
        if (columna is "OBJETOS" or "OBSERVACION" && v[columna] is string texto)
            _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
    }

    /// <summary>Al elegir un respaldo se llenan sus esquemas y tablas.</summary>
    private async Task ElegirRespaldoAsync()
    {
        if (_ocupado) return;
        var r = Respaldo;
        _clbEsquemas.Items.Clear();
        _cboEsquema.Items.Clear();
        _clbTablas.Items.Clear();
        if (r is not null)
        {
            var esquemas = r.Esquemas(_esquemasOracle);
            foreach (var e in esquemas) _clbEsquemas.Items.Add(e, esquemas.Count == 1);
            _cboEsquema.Items.AddRange(esquemas.ToArray<object>());
            if (_cboEsquema.Items.Count > 0) _cboEsquema.SelectedIndex = 0;
            if (!_chkRemap.Checked) _txtDestino.Text = "";

            _rdoEsquemas.Enabled = r.Tipo != TipoRespaldo.Table;
            if (!_rdoEsquemas.Enabled && _rdoEsquemas.Checked) _rdoTablas.Checked = true;
            _lblTodo.Text = r.Tipo switch
            {
                TipoRespaldo.Full => "Import FULL de todo el respaldo. Lo que ya existe en la base se omite o da error\r\n" +
                                     "(se verá en el log). Para la demo es mejor restaurar algunos esquemas o tablas.",
                TipoRespaldo.Schema => $"Import SCHEMA de: {string.Join(", ", r.Objetos.Select(o => o.Esquema))}.",
                _ => $"Import TABLE de: {string.Join(", ", r.Objetos.Select(o => $"{o.Esquema}.{o.Tabla}"))}."
            };
            _lblEstado.Text = r.ExisteEnDisco
                ? $"Respaldo {r.ArchivoDump} ({r.Tipo.ToString().ToUpperInvariant()}). Elija qué restaurar y presione «Restaurar»."
                : $"El archivo {r.ArchivoDump} ya no está en {r.RutaSo}: este respaldo no se puede restaurar.";
        }
        ActualizarBotones();
        await CargarTablasAsync();
    }

    /// <summary>Tablas del esquema elegido: las que trae el respaldo según su log (o, si no hay log, las que existen hoy).</summary>
    private async Task CargarTablasAsync()
    {
        _clbTablas.Items.Clear();
        if (Respaldo is not { } r || _cboEsquema.SelectedItem is not string esquema) return;
        var tablas = r.Tipo == TipoRespaldo.Table
            ? r.Objetos.Where(o => o.Esquema == esquema && o.Tabla is not null).Select(o => o.Tabla!).ToList()
            : LogDataPump.TablasDeArchivo(r.RutaLog).Where(t => t.Esquema == esquema).Select(t => t.Tabla).OrderBy(t => t).ToList();
        if (tablas.Count == 0)
        {
            try
            {
                tablas = (await Bd.ConsultarAsync(SqlRecuperacion.TablasDe, Parametro.De("e", esquema))).AsEnumerable().Select(f => (string)f[0]).ToList();
            }
            catch (Exception ex) { Dialogos.Error(this, ex); }
        }
        foreach (var t in tablas.Distinct()) _clbTablas.Items.Add(t);
    }

    // ------------------------------------------------------------------ comportamiento

    private void CambiarAlcance()
    {
        _pnlEsquemas.Visible = Alcance == AlcanceRestauracion.Esquemas;
        _pnlTablas.Visible = Alcance == AlcanceRestauracion.Tablas;
        _lblTodo.Visible = Alcance == AlcanceRestauracion.Todo;
    }

    private void AgregarTabla()
    {
        try
        {
            var tabla = Identificador.Validar(_txtOtraTabla.Text, "tabla");
            var i = _clbTablas.Items.IndexOf(tabla);
            if (i < 0) i = _clbTablas.Items.Add(tabla);
            _clbTablas.SetItemChecked(i, true);
            _txtOtraTabla.Clear();
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private void ActualizarBotones()
    {
        var listo = !_ocupado && Respaldo is { ExisteEnDisco: true };
        _btnRestaurar.Enabled = _btnVerSql.Enabled = listo;
        _btnRefrescar.Enabled = !_ocupado;
    }

    private SolicitudRestauracion Solicitud()
    {
        if (Respaldo is not { } r) throw new ArgumentException("Elija un respaldo del catálogo.");
        var esquemas = Alcance switch
        {
            AlcanceRestauracion.Esquemas => _clbEsquemas.CheckedItems.Cast<string>().ToList(),
            AlcanceRestauracion.Tablas when _cboEsquema.SelectedItem is string e => [e],
            _ => new List<string>()
        };
        var tablas = Alcance == AlcanceRestauracion.Tablas ? _clbTablas.CheckedItems.Cast<string>().ToList() : [];
        var destino = _chkRemap.Checked ? _txtDestino.Text.Trim() : null;
        if (_chkRemap.Checked && string.IsNullOrEmpty(destino))
            throw new ArgumentException("Escriba el esquema destino o desmarque «Restaurar en otro esquema».");
        var s = new SolicitudRestauracion(r, Alcance, esquemas, tablas, ((OpcionAccion)_cboAccion.SelectedItem!).Accion, destino, DateTime.Now);
        ServicioRecuperacion.Validar(s);
        return s;
    }

    private void VerSql()
    {
        try
        {
            var s = Solicitud();
            Dialogos.VerSql(this, $"Restaurar {s.Modo} con DBMS_DATAPUMP", ServicioRecuperacion.ArmarBloque(s));
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task RestaurarAsync()
    {
        SolicitudRestauracion s;
        try
        {
            s = Solicitud();
            if (!Dialogos.ConfirmarSql(this, $"Restaurar {s.Modo} con DBMS_DATAPUMP", ServicioRecuperacion.ArmarBloque(s), s.Destructiva)) return;
        }
        catch (Exception ex) { Dialogos.Error(this, ex); return; }

        Ocupado(true, $"Restaurando… job {s.Job} en ejecución");
        _lblResultado.Text = "";
        ResultadoRestauracion? resultado = null;
        try
        {
            resultado = await new ServicioRecuperacion(Bd).EjecutarAsync(s);
            MostrarResultado(s, resultado);
            DlgLog.Mostrar(this, Path.Combine(s.Respaldo.RutaSo, s.ArchivoLog));
        }
        catch (Exception ex)
        {
            _lblEstado.Text = $"La restauración {s.Job} falló: quedó en ERROR en el historial.";
            Dialogos.Error(this, ex);
        }
        finally
        {
            Ocupado(false, null);
            RestauracionTerminada?.Invoke(this, resultado);
        }
    }

    /// <summary>Verificación: cuántas filas quedaron en cada tabla restaurada, y avisos del log.</summary>
    private void MostrarResultado(SolicitudRestauracion s, ResultadoRestauracion r)
    {
        var lineas = r.Conteos.Select(c => $"{c.Tabla}: {(c.Filas is { } n ? $"{n:N0} filas" : "no existe")}").ToList();
        if (r.Conteos.Count == 0) lineas.Add("Revise el log para ver lo que se importó.");
        if (r.TablasOmitidas > 0) lineas.Add($"⚠ {r.TablasOmitidas} tabla(s) omitida(s) porque ya existían (SKIP).");
        if (r.ErroresLog > 0) lineas.Add($"⚠ {r.ErroresLog} línea(s) con ORA- en el log.");
        _lblResultado.Text = "Resultado\r\n" + string.Join("\r\n", lineas);
        _lblResultado.ForeColor = r.ErroresLog > 0 || r.TablasOmitidas > 0 ? Tema.Advertencia : Tema.Ok;

        _lblEstado.Text = $"Restauración {s.Job}: {r.EstadoJob}";
        if (!r.Completado)
            Toast.Mostrar(this, $"La restauración terminó en estado {r.EstadoJob}. Revise el log.", TipoEstado.Advertencia);
        else if (r.ErroresLog > 0 || r.TablasOmitidas > 0)
            Toast.Mostrar(this, "La restauración terminó, pero con avisos en el log.", TipoEstado.Advertencia);
        else
            Dialogos.Exito(this, $"Restauración completada: {string.Join(" · ", lineas.Take(3))}");
    }

    private void Ocupado(bool ocupado, string? mensaje)
    {
        _ocupado = ocupado;
        UseWaitCursor = ocupado;
        foreach (Control c in new Control[] { _grilla, _rdoTodo, _rdoEsquemas, _rdoTablas, _clbEsquemas, _cboEsquema, _clbTablas, _txtOtraTabla, _cboAccion, _chkRemap, _txtDestino })
            c.Enabled = !ocupado;
        if (!ocupado)
        {
            _txtDestino.Enabled = _chkRemap.Checked;
            _rdoEsquemas.Enabled = Respaldo?.Tipo != TipoRespaldo.Table;
        }
        if (ocupado) _barra.Iniciar(); else _barra.Detener();
        if (mensaje is not null) _lblEstado.Text = mensaje;
        ActualizarBotones();
    }
}

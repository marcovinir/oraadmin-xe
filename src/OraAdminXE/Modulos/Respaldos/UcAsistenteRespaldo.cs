using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Pestaña «Nuevo respaldo» (issue #11): asistente en un solo panel para respaldar Full, por esquema o por tabla
/// con DBMS_DATAPUMP. A la izquierda qué se respalda; a la derecha dónde se guarda; abajo Ver SQL y Ejecutar.
/// </summary>
public sealed class UcAsistenteRespaldo : UserControl
{
    private readonly RadioButton _rdoFull = Opcion("Full", "Toda la base de datos (PDB XEPDB1)");
    private readonly RadioButton _rdoEsquema = Opcion("Por esquema", "Uno o varios usuarios con todos sus objetos");
    private readonly RadioButton _rdoTabla = Opcion("Por tabla", "Algunas tablas de un esquema");
    private readonly CheckedListBox _clbEsquemas = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly ComboBox _cboEsquemaTablas = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly CheckedListBox _clbTablas = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly Label _lblFull = new()
    {
        Dock = DockStyle.Fill, ForeColor = Tema.Advertencia, Padding = new Padding(4, 8, 4, 0),
        Text = "Respalda toda la PDB XEPDB1: todos los esquemas de usuario con sus datos, más usuarios,\r\nroles, perfiles y directorios. Tarda un par de minutos (la pantalla sigue respondiendo)."
    };
    private readonly FlowLayoutPanel _marcar = new() { Dock = DockStyle.Bottom, Height = 34, WrapContents = false };
    private readonly Panel _pnlEsquemas = new() { Dock = DockStyle.Fill };
    private readonly Panel _pnlTablas = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cboDirectorio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly TextBox _txtNombre = new() { CharacterCasing = CharacterCasing.Upper, Dock = DockStyle.Fill, MaxLength = 120 };
    private readonly Label _lblArchivos = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, Padding = new Padding(0, 6, 0, 0) };
    private readonly TextBox _txtObservacion = new() { Dock = DockStyle.Fill, Multiline = true, MaxLength = 300, ScrollBars = ScrollBars.Vertical };
    private readonly BarraProgreso _barra = new() { Dock = DockStyle.Top, Visible = false };
    private readonly Label _lblEstado = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnVerSql = Tema.Boton("Ver SQL", icono: "");
    private readonly BotonPlano _btnEjecutar = Tema.Boton("Ejecutar respaldo", primario: true, icono: "");
    private bool _nombreEditado;
    private bool _ocupado;
    private readonly System.Windows.Forms.Timer _progreso = new() { Interval = 2000 };
    private string? _jobEnCurso;
    private bool _consultandoAvance;

    private static IBaseDatos Bd => Sesion.Bd;

    /// <summary>Se dispara al empezar un respaldo (el catálogo no deja limpiar jobs mientras tanto).</summary>
    public event EventHandler? RespaldoIniciado;

    /// <summary>
    /// Se dispara al terminar un respaldo, bien o mal, para recargar el catálogo y abrir el log.
    /// El resultado es null si Oracle lanzó un error (el respaldo quedó en ERROR).
    /// </summary>
    public event EventHandler<(SolicitudRespaldo Solicitud, ResultadoRespaldo? Resultado)>? RespaldoTerminado;

    public UcAsistenteRespaldo()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(20, 16, 20, 12);
        Font = Tema.Fuente;

        // ---- Izquierda: qué respaldar
        var tipos = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        tipos.Controls.AddRange([_rdoEsquema, _rdoTabla, _rdoFull]);

        _marcar.Controls.AddRange([Asistente.Enlace("Marcar todos", () => Marcar(true)), Asistente.Enlace("Ninguno", () => Marcar(false))]);
        _pnlEsquemas.Controls.Add(Asistente.Marco(_clbEsquemas));
        var esquemaTablas = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, 0, 0, 8) };
        esquemaTablas.Controls.Add(_cboEsquemaTablas);
        esquemaTablas.Controls.Add(new Label { Text = "Esquema", AutoSize = false, Width = 76, Dock = DockStyle.Left, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 4, 0, 0) });
        _pnlTablas.Controls.Add(Asistente.Marco(_clbTablas));
        _pnlTablas.Controls.Add(esquemaTablas);
        var objetos = new Panel { Dock = DockStyle.Fill };
        objetos.Controls.AddRange([_pnlEsquemas, _pnlTablas, _lblFull, _marcar]);

        var izquierda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 20, 0) };
        izquierda.Controls.Add(objetos);
        izquierda.Controls.Add(Asistente.Seccion("2", "Objetos", "Marque lo que quiere respaldar"));
        izquierda.Controls.Add(tipos);
        izquierda.Controls.Add(Asistente.Seccion("1", "Tipo de respaldo", "Modo de Data Pump (job_mode)"));

        // ---- Derecha: dónde guardarlo
        var nombre = new Panel { Dock = DockStyle.Top, Height = 28 };
        nombre.Controls.Add(_txtNombre);
        var sugerir = Asistente.Enlace("Sugerir otro nombre", () => SugerirNombre());
        sugerir.Dock = DockStyle.Right;
        sugerir.Padding = new Padding(0, 6, 0, 0);
        var archivos = new Panel { Dock = DockStyle.Top, Height = 58 };
        archivos.Controls.Add(_lblArchivos);
        archivos.Controls.Add(sugerir);
        var observacion = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 4) };
        observacion.Controls.Add(_txtObservacion);

        var derecha = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 0, 0) };
        derecha.Controls.Add(observacion);
        derecha.Controls.Add(Asistente.Seccion("5", "Observación", "Opcional: queda en el catálogo"));
        derecha.Controls.Add(archivos);
        derecha.Controls.Add(nombre);
        derecha.Controls.Add(Asistente.Seccion("4", "Nombre del respaldo", "Es el nombre del job y de los archivos"));
        derecha.Controls.Add(Asistente.Espacio(12));
        derecha.Controls.Add(_cboDirectorio);
        derecha.Controls.Add(Asistente.Seccion("3", "Directorio", "Objeto DIRECTORY donde Oracle escribe el .DMP"));
        Asistente.Divisoria(derecha, arriba: false);

        var division = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        division.Controls.Add(izquierda, 0, 0);
        division.Controls.Add(derecha, 1, 0);

        // ---- Abajo: progreso y acciones
        var botones = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
        botones.Controls.AddRange([_btnEjecutar, _btnVerSql]);
        var estado = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 24, 0) };
        estado.Controls.Add(_lblEstado);
        estado.Controls.Add(_barra);
        var pie = new Panel { Dock = DockStyle.Bottom, Height = 62 };
        pie.Controls.Add(estado);
        pie.Controls.Add(botones);
        Asistente.Divisoria(pie, arriba: true);

        Controls.Add(division);
        Controls.Add(pie);

        foreach (var rdo in new[] { _rdoFull, _rdoEsquema, _rdoTabla })
            rdo.CheckedChanged += (_, _) => { if (rdo.Checked) CambiarTipo(); };
        _cboEsquemaTablas.SelectedIndexChanged += async (_, _) => await CargarTablasAsync();
        _cboDirectorio.SelectedIndexChanged += (_, _) => ActualizarArchivos();
        _txtNombre.TextChanged += (_, _) => ActualizarArchivos();
        _txtNombre.KeyPress += (_, _) => _nombreEditado = true;
        _btnVerSql.Click += (_, _) => VerSql();
        _btnEjecutar.Click += async (_, _) => await EjecutarAsync();
        _progreso.Tick += async (_, _) => await ConsultarAvanceAsync();
        _rdoEsquema.Checked = true;
    }

    private static RadioButton Opcion(string texto, string detalle) =>
        new() { Text = $"{texto}   —   {detalle}", AutoSize = true, Margin = new Padding(2, 2, 0, 4), Tag = texto };

    // ------------------------------------------------------------------ datos

    private TipoRespaldo Tipo => _rdoFull.Checked ? TipoRespaldo.Full : _rdoTabla.Checked ? TipoRespaldo.Table : TipoRespaldo.Schema;

    /// <summary>Carga directorios y esquemas (conserva lo que ya estaba marcado).</summary>
    public async Task CargarAsync()
    {
        if (string.IsNullOrEmpty(Bd.Usuario) || _ocupado) return;
        try
        {
            var directorios = Bd.ConsultarAsync(SqlRespaldos.Directorios);
            var esquemas = Bd.ConsultarAsync(SqlRespaldos.Esquemas);
            await Task.WhenAll(directorios, esquemas);

            var dirAnterior = (_cboDirectorio.SelectedItem as DirectorioRespaldo)?.NombreOracle ?? "DIR_RESPALDOS";
            _cboDirectorio.Items.Clear();
            foreach (DataRow r in directorios.Result.Rows)
                _cboDirectorio.Items.Add(new DirectorioRespaldo(Convert.ToInt32(r["ID_DIRECTORIO"]), (string)r["NOMBRE_ORACLE"], (string)r["RUTA_SO"]));
            _cboDirectorio.SelectedItem = _cboDirectorio.Items.Cast<DirectorioRespaldo>().FirstOrDefault(d => d.NombreOracle == dirAnterior)
                                          ?? _cboDirectorio.Items.Cast<DirectorioRespaldo>().FirstOrDefault();

            var nombres = esquemas.Result.Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();
            var marcados = _clbEsquemas.CheckedItems.Cast<string>().ToHashSet();
            if (marcados.Count == 0) marcados.Add("DEMO_VENTAS");
            _clbEsquemas.Items.Clear();
            foreach (var n in nombres) _clbEsquemas.Items.Add(n, marcados.Contains(n));

            var esquemaTablas = _cboEsquemaTablas.SelectedItem as string ?? "DEMO_VENTAS";
            _cboEsquemaTablas.Items.Clear();
            _cboEsquemaTablas.Items.AddRange(nombres.ToArray<object>());
            _cboEsquemaTablas.SelectedItem = nombres.Contains(esquemaTablas) ? esquemaTablas : nombres.FirstOrDefault();

            if (!_nombreEditado) SugerirNombre();
            _lblEstado.Text = _cboDirectorio.Items.Count == 0
                ? "No hay directorios activos en ADMIN_BD.DIRECTORIO. Cree uno en «Directorios y archivos»."
                : "Elija qué respaldar y presione «Ejecutar respaldo».";
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        ActualizarBotones();
    }

    private async Task CargarTablasAsync()
    {
        _clbTablas.Items.Clear();
        if (_cboEsquemaTablas.SelectedItem is not string esquema) return;
        try
        {
            var tablas = await Bd.ConsultarAsync(SqlRespaldos.TablasDe, Parametro.De("e", esquema));
            foreach (DataRow r in tablas.Rows) _clbTablas.Items.Add((string)r[0]);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    // ------------------------------------------------------------------ comportamiento

    private void CambiarTipo()
    {
        _pnlEsquemas.Visible = Tipo == TipoRespaldo.Schema;
        _pnlTablas.Visible = Tipo == TipoRespaldo.Table;
        _lblFull.Visible = Tipo == TipoRespaldo.Full;
        _marcar.Visible = Tipo != TipoRespaldo.Full;
        if (!_nombreEditado) SugerirNombre();
    }

    private void Marcar(bool marcar)
    {
        var lista = Tipo == TipoRespaldo.Table ? _clbTablas : _clbEsquemas;
        for (var i = 0; i < lista.Items.Count; i++) lista.SetItemChecked(i, marcar);
    }

    private void SugerirNombre()
    {
        _txtNombre.Text = ServicioRespaldos.NombreSugerido(Tipo, DateTime.Now);
        _nombreEditado = false;
    }

    private void ActualizarArchivos()
    {
        var nombre = _txtNombre.Text.Trim();
        var ruta = (_cboDirectorio.SelectedItem as DirectorioRespaldo)?.RutaSo ?? "…";
        _lblArchivos.Text = nombre.Length == 0 ? "" : $"Archivos: {nombre}.DMP y .LOG\r\nCarpeta: {ruta}";
    }

    private void ActualizarBotones()
    {
        var listo = !_ocupado && !string.IsNullOrEmpty(Bd.Usuario) && _cboDirectorio.SelectedItem is not null;
        _btnEjecutar.Enabled = _btnVerSql.Enabled = listo;
    }

    /// <summary>Arma la solicitud con lo elegido (lanza ArgumentException si falta algo).</summary>
    private SolicitudRespaldo Solicitud()
    {
        if (_cboDirectorio.SelectedItem is not DirectorioRespaldo dir)
            throw new ArgumentException("Elija el directorio donde se guardará el respaldo.");
        var esquemas = Tipo switch
        {
            TipoRespaldo.Schema => _clbEsquemas.CheckedItems.Cast<string>().ToList(),
            TipoRespaldo.Table when _cboEsquemaTablas.SelectedItem is string e => [e],
            _ => new List<string>()
        };
        var tablas = Tipo == TipoRespaldo.Table ? _clbTablas.CheckedItems.Cast<string>().ToList() : [];
        var s = new SolicitudRespaldo(Tipo, dir, _txtNombre.Text, esquemas, tablas, _txtObservacion.Text);
        ServicioRespaldos.Validar(s);
        return s;
    }

    private void VerSql()
    {
        try
        {
            var s = Solicitud();
            Dialogos.VerSql(this, $"Respaldo {s.Codigo} con DBMS_DATAPUMP", ServicioRespaldos.ArmarBloque(s));
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EjecutarAsync()
    {
        SolicitudRespaldo s;
        var servicio = new ServicioRespaldos(Bd);
        try
        {
            s = Solicitud();
            await servicio.ValidarDisponibleAsync(s);
            if (!Dialogos.ConfirmarSql(this, $"Respaldo {s.Codigo} con DBMS_DATAPUMP", ServicioRespaldos.ArmarBloque(s))) return;
        }
        catch (Exception ex) { Dialogos.Error(this, ex); return; }

        Ocupado(true, $"Respaldando… job {s.Job} en ejecución" + (s.Tipo == TipoRespaldo.Full ? " (el full tarda unos minutos)" : ""));
        _jobEnCurso = s.Job;
        _progreso.Start();
        RespaldoIniciado?.Invoke(this, EventArgs.Empty);
        ResultadoRespaldo? resultado = null;
        try
        {
            resultado = await servicio.EjecutarAsync(s);
            if (resultado.Completado)
            {
                _lblEstado.Text = $"Último respaldo: {s.ArchivoDump} · {resultado.TamanoMb:0.##} MB · COMPLETADO";
                Dialogos.Exito(this, $"Respaldo {s.Codigo} completado: {s.ArchivoDump} ({resultado.TamanoMb:0.##} MB).");
            }
            else
            {
                _lblEstado.Text = $"El job {s.Job} terminó en estado {resultado.EstadoJob}.";
                Toast.Mostrar(this, $"El respaldo terminó en estado {resultado.EstadoJob}. Revise el log.", TipoEstado.Advertencia);
            }
        }
        catch (Exception ex)
        {
            _lblEstado.Text = $"El respaldo {s.Job} falló: quedó en ERROR en el catálogo.";
            Dialogos.Error(this, ex);
        }
        finally
        {
            _progreso.Stop();
            _jobEnCurso = null;
            Ocupado(false, null);
            SugerirNombre();
            RespaldoTerminado?.Invoke(this, (s, resultado));
        }
    }

    /// <summary>
    /// Progreso en vivo (issue #12): por OTRA sesión del pool consulta el estado del job y su avance
    /// en V$SESSION_LONGOPS. Si no hay avance medible, la barra sigue animada.
    /// </summary>
    private async Task ConsultarAvanceAsync()
    {
        if (_jobEnCurso is not { } job || _consultandoAvance) return;
        _consultandoAvance = true;
        try
        {
            var estado = Bd.EscalarAsync(SqlRespaldos.EstadoJob, Parametro.De("job", job));
            var avance = Bd.ConsultarAsync(SqlRespaldos.Avance, Parametro.De("job", job));
            await Task.WhenAll(estado, avance);
            if (_jobEnCurso != job) return;   // ya terminó mientras consultábamos

            var fila = avance.Result.Rows.Count > 0 ? avance.Result.Rows[0] : null;
            int? pct = fila is null || fila["PCT"] is DBNull ? null : Convert.ToInt32(fila["PCT"]);
            _barra.Porcentaje = pct;
            _lblEstado.Text = $"Job {job}: {estado.Result ?? "iniciando"}" + (pct is null ? "…" : $" · {pct} %");
        }
        catch (Exception)
        {
            // El progreso es informativo: si una consulta falla, se reintenta en el siguiente tic.
        }
        finally
        {
            _consultandoAvance = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _progreso.Dispose();
        base.Dispose(disposing);
    }

    private void Ocupado(bool ocupado, string? mensaje)
    {
        _ocupado = ocupado;
        UseWaitCursor = ocupado;
        foreach (Control c in new Control[] { _rdoFull, _rdoEsquema, _rdoTabla, _clbEsquemas, _cboEsquemaTablas, _clbTablas, _cboDirectorio, _txtNombre, _txtObservacion })
            c.Enabled = !ocupado;
        if (ocupado) _barra.Iniciar(); else _barra.Detener();
        if (mensaje is not null) _lblEstado.Text = mensaje;
        ActualizarBotones();
    }
}

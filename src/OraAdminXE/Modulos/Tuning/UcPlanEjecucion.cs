using System.Data;
using System.Globalization;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Dashboard;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Pestaña Plan de ejecución (issue #21, P4): editor de consulta, plan estimado (EXPLAIN PLAN),
/// plan real (GATHER_PLAN_STATISTICS) y top 10 de SQL por tiempo con «Analizar».
/// </summary>
public sealed class UcPlanEjecucion : UserControl
{
    private readonly TextBox _txtConsulta = new()
    {
        Dock = DockStyle.Top, Height = 96, Multiline = true, AcceptsReturn = true, AcceptsTab = true,
        ScrollBars = ScrollBars.Vertical, Font = Tema.FuenteCodigo, Text = SqlTuning.ConsultaDemo,
        BackColor = Tema.SuperficieAlta, ForeColor = Tema.Texto, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly BotonPlano _btnEstimado = Tema.Boton("Plan estimado", primario: true);
    private readonly BotonPlano _btnReal = Tema.Boton("Plan real");
    private readonly Tarjeta _tarCosto = Crear("Costo", 140);
    private readonly Tarjeta _tarFilasEst = Crear("Filas estimadas", 160);
    private readonly Tarjeta _tarOperacion = Crear("Operación principal", 330);
    private readonly Tarjeta _tarFilasReal = Crear("Filas devueltas", 160);
    private readonly Tarjeta _tarTiempo = Crear("Tiempo", 130);
    private readonly RichTextBox _txtPlan = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both,
        Font = Tema.FuenteCodigo, BackColor = Tema.Superficie, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None,
        DetectUrls = false
    };
    private readonly DataGridView _grdTopSql = new() { Dock = DockStyle.Fill };
    private readonly BotonPlano _btnAntes = Tema.Boton("Fijar como ANTES");
    private readonly BotonPlano _btnQuitarAntes = Tema.Boton("Quitar ANTES");
    private readonly RichTextBox _txtAntes = CrearVisor();
    private readonly RichTextBox _txtDespues = CrearVisor();
    private readonly Tarjeta _tarResumen = new() { Titulo = "Costo antes → después", Dock = DockStyle.Top, Height = 100, Margin = new Padding(0) };
    private readonly Pestanas _subpestanas = new() { Dock = DockStyle.Fill };
    private readonly BotonPlano _btnAnalizar = Tema.Boton("Analizar");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar");

    private (ResultadoPlan Plan, string Consulta)? _ultimoEstimado;   // último «Plan estimado» (candidato a ANTES)
    private ResultadoPlan? _antes;             // plan fijado como ANTES
    private string? _consultaAntes;            // consulta con la que se generó ANTES
    private bool _ejecutando;
    private bool _cargandoTop;
    private bool _analizando;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcPlanEjecucion()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        // En una ventana muy baja o angosta aparecen barras de desplazamiento en vez de recortar el plan o las tarjetas.
        AutoScroll = true;
        AutoScrollMinSize = new Size(1000, 500);

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        barra.Controls.AddRange([_btnEstimado, _btnReal, _btnAntes, _btnQuitarAntes]);

        var tarjetas = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 108, WrapContents = false, Padding = new Padding(0, 4, 0, 4) };
        tarjetas.Controls.AddRange([_tarCosto, _tarFilasEst, _tarOperacion, _tarFilasReal, _tarTiempo]);

        var barraTop = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        barraTop.Controls.AddRange([_btnAnalizar, _btnRefrescar]);
        var topSql = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        topSql.Controls.Add(_grdTopSql);
        topSql.Controls.Add(barraTop);

        // Plan y Top SQL comparten el espacio que dejan editor y tarjetas, cada uno en su sub-pestaña,
        // así el plan siempre tiene altura aunque la ventana sea pequeña.
        _subpestanas.Agregar("Plan", _txtPlan);
        _subpestanas.Agregar("Antes / después", CrearComparador());
        _subpestanas.Agregar("Top 10 SQL por tiempo", topSql);

        // El último control agregado se acopla primero: de arriba abajo quedan título, editor, botones, tarjetas y sub-pestañas.
        Controls.Add(_subpestanas);
        Controls.Add(tarjetas);
        Controls.Add(barra);
        Controls.Add(_txtConsulta);
        Controls.Add(new Label
        {
            Text = "Consulta (solo SELECT o WITH)", Dock = DockStyle.Top, Height = 32,
            ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft
        });

        Tema.AplicarGrilla(_grdTopSql);
        Tema.MensajeVacio(_grdTopSql, "Sin consultas en la caché de Oracle", "\uE9D9");
        _grdTopSql.MultiSelect = false;

        _btnEstimado.Click += async (_, _) => await PedirPlanAsync(real: false);
        _btnReal.Click += async (_, _) => await PedirPlanAsync(real: true);
        _btnAntes.Click += (_, _) => FijarAntes();
        _btnQuitarAntes.Click += (_, _) => QuitarAntes();
        _btnRefrescar.Click += async (_, _) => await CargarTopSqlAsync();
        _btnAnalizar.Click += async (_, _) => await AnalizarAsync();
        _grdTopSql.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await AnalizarAsync(); };
        _grdTopSql.SelectionChanged += (_, _) => ActualizarBotones();
        // Ctrl+Enter pide el plan estimado sin soltar el teclado.
        _txtConsulta.KeyDown += async (_, e) =>
        {
            if (e is not { Control: true, KeyCode: Keys.Enter }) return;
            e.SuppressKeyPress = true;
            await PedirPlanAsync(real: false);
        };

        LimpiarTarjetas();
        ActualizarBotones();
    }

    private bool PuedeActualizarUi => !IsDisposed && !Disposing;

    /// <summary>Carga el top SQL; lo llama <see cref="UcTuning"/> al abrir la pestaña.</summary>
    public Task CargarAsync() => CargarTopSqlAsync();

    // ------------------------------------------------------------------ plan

    private async Task PedirPlanAsync(bool real)
    {
        if (_ejecutando) return;
        try
        {
            var consulta = ServicioPlanes.Validar(_txtConsulta.Text);
            _ejecutando = true;
            ActualizarBotones();
            UseWaitCursor = true;
            var resultado = real
                ? await ServicioPlanes.EjecutarAsync(Bd, consulta)
                : await ServicioPlanes.EstimarAsync(Bd, consulta);
            if (!PuedeActualizarUi) return;
            MostrarPlan(resultado);
            if (!real) _ultimoEstimado = (resultado, consulta);
            // Con un ANTES fijado, el estimado es el DESPUÉS: se enseña de una vez al lado del ANTES.
            if (!real && _antes is not null)
            {
                MostrarDespues(resultado, consulta);
                _subpestanas.Seleccionar(1);
            }
            else _subpestanas.Seleccionar(0);
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi) Dialogos.Error(this, ex);
        }
        finally
        {
            _ejecutando = false;
            if (PuedeActualizarUi)
            {
                UseWaitCursor = false;
                ActualizarBotones();
            }
        }
    }

    private void MostrarPlan(ResultadoPlan r)
    {
        ResaltarOperacion(_txtPlan, r.Texto);

        _tarCosto.Valor = r.Costo is { } c ? c.ToString("N0", CultureInfo.CurrentCulture) : "—";
        _tarCosto.Detalle = r.Costo is null ? "solo en plan estimado" : "costo total del plan";
        _tarFilasEst.Valor = r.FilasEstimadas is { } f ? f.ToString("N0", CultureInfo.CurrentCulture) : "—";
        _tarFilasEst.Detalle = r.FilasEstimadas is null ? "solo en plan estimado" : "cardinalidad de la raíz";
        _tarOperacion.Valor = r.Operacion ?? "—";
        _tarOperacion.Detalle = r.Operacion is null ? "el plan no accede a tablas" : "primer acceso a datos";
        var acceso = (r.Operacion ?? "").Contains("FULL", StringComparison.OrdinalIgnoreCase);
        _tarOperacion.Acento = r.Operacion is null ? Tema.Borde : acceso ? Tema.Advertencia : Tema.Ok;

        _tarFilasReal.Valor = r.FilasDevueltas is { } n ? n.ToString("N0", CultureInfo.CurrentCulture) : "—";
        _tarFilasReal.Detalle = r.FilasDevueltas is null ? "solo en plan real" : "filas leídas hasta el final";
        _tarTiempo.Valor = r.Tiempo is { } t ? $"{t.TotalSeconds:0.00} s" : "—";
        _tarTiempo.Detalle = r.Tiempo is null ? "solo en plan real" : "ejecución + lectura";
    }

    /// <summary>Pinta la línea de la operación principal (TABLE ACCESS FULL, INDEX…) para que se vea de un vistazo.</summary>
    private static void ResaltarOperacion(RichTextBox caja, string texto)
    {
        caja.Text = texto;
        caja.SelectAll();
        caja.SelectionBackColor = Tema.Superficie;
        caja.SelectionColor = Tema.Texto;

        var linea = ServicioPlanes.LineaOperacionPrincipal(texto);
        var inicio = linea is null ? -1 : caja.Text.IndexOf(linea, StringComparison.Ordinal);
        if (linea is not null && inicio >= 0)
        {
            caja.Select(inicio, linea.Length);
            var acceso = linea.Contains("FULL", StringComparison.OrdinalIgnoreCase);
            caja.SelectionBackColor = acceso ? Tema.AdvertenciaFondo : Tema.OkFondo;
            caja.SelectionColor = acceso ? Tema.Advertencia : Tema.Ok;
        }
        caja.Select(0, 0);
        caja.ScrollToCaret();
    }

    // ------------------------------------------------------------------ comparador antes / después

    private static RichTextBox CrearVisor() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both,
        Font = Tema.FuenteCodigo, BackColor = Tema.Superficie, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None,
        DetectUrls = false
    };

    /// <summary>Resumen del costo arriba; abajo el plan ANTES a la izquierda y el DESPUÉS a la derecha.</summary>
    private Control CrearComparador()
    {
        var cuerpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cuerpo.Controls.Add(Etiqueta("ANTES"), 0, 0);
        cuerpo.Controls.Add(Etiqueta("DESPUÉS"), 1, 0);
        cuerpo.Controls.Add(_txtAntes, 0, 1);
        cuerpo.Controls.Add(_txtDespues, 1, 1);

        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(cuerpo);
        panel.Controls.Add(_tarResumen);
        LimpiarComparador();
        return panel;

        static Label Etiqueta(string texto) => new()
        {
            Text = texto, Dock = DockStyle.Fill, Font = Tema.FuenteNegrita, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft
        };
    }

    /// <summary>Guarda el último plan estimado como ANTES. Hace falta un estimado: trae el costo que se compara.</summary>
    private void FijarAntes()
    {
        if (_ultimoEstimado is not { } estimado)
        {
            Dialogos.Error(this, new InvalidOperationException("Pida primero un «Plan estimado» para fijarlo como ANTES."));
            return;
        }
        _antes = estimado.Plan;
        _consultaAntes = estimado.Consulta;
        ResaltarOperacion(_txtAntes, estimado.Plan.Texto);
        _txtDespues.Clear();
        _tarResumen.Valor = $"{estimado.Plan.Costo:0}";
        _tarResumen.Detalle = "ANTES fijado: haga el cambio y pida otro Plan estimado";
        _tarResumen.Acento = Tema.Info;
        ActualizarBotones();
        _subpestanas.Seleccionar(1);
        Dialogos.Exito(this, $"Plan fijado como ANTES (costo {estimado.Plan.Costo:0}).");
    }

    private void QuitarAntes()
    {
        _antes = null;
        _consultaAntes = null;
        LimpiarComparador();
        ActualizarBotones();
    }

    private void MostrarDespues(ResultadoPlan despues, string consultaActual)
    {
        ResaltarOperacion(_txtDespues, despues.Texto);
        var mismaConsulta = _consultaAntes is null ||
            string.Equals(_consultaAntes, consultaActual, StringComparison.Ordinal);
        var avisoModificada = mismaConsulta ? "" : " (consulta modificada)";

        var resumen = _antes is null ? null : ServicioPlanes.CompararCostos(_antes, despues);
        if (resumen is null)
        {
            _tarResumen.Valor = "—";
            _tarResumen.Detalle = "no hay costo para comparar" + avisoModificada;
            _tarResumen.Acento = Tema.Borde;
            return;
        }
        _tarResumen.Valor = resumen.Valor;
        _tarResumen.Detalle = resumen.Detalle + avisoModificada;
        _tarResumen.Acento = resumen.Mejora ? Tema.Ok : resumen.Empeora ? Tema.Peligro : Tema.Borde;
    }

    private void LimpiarComparador()
    {
        _txtAntes.Clear();
        _txtDespues.Clear();
        _tarResumen.Valor = "—";
        _tarResumen.Detalle = "pida un Plan estimado y fíjelo como ANTES";
        _tarResumen.Acento = Tema.Borde;
    }

    private void LimpiarTarjetas()
    {
        foreach (var t in new[] { _tarCosto, _tarFilasEst, _tarOperacion, _tarFilasReal, _tarTiempo })
        {
            t.Valor = "—";
            t.Detalle = "";
        }
        _tarCosto.Detalle = "pida un plan";
    }

    // ------------------------------------------------------------------ top SQL

    private async Task CargarTopSqlAsync()
    {
        if (_cargandoTop || !PuedeActualizarUi || string.IsNullOrEmpty(Bd.Usuario)) return;
        _cargandoTop = true;
        ActualizarBotones();
        Tema.Cargando(_grdTopSql, true);
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlTuning.TopSql);
            if (!PuedeActualizarUi) return;
            _grdTopSql.DataSource = tabla;
            var columnas = new Dictionary<string, (string Texto, float Peso)>
            {
                ["SQL_ID"] = ("SQL_ID", 13), ["EXECUTIONS"] = ("Ejecuciones", 9), ["SEG"] = ("Segundos", 8),
                ["BUFFER_GETS"] = ("Buffer gets", 10), ["DISK_READS"] = ("Lecturas disco", 10), ["TEXTO"] = ("Consulta", 50)
            };
            foreach (DataGridViewColumn col in _grdTopSql.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                if (columnas.TryGetValue(col.Name, out var c))
                {
                    col.HeaderText = c.Texto;
                    col.FillWeight = c.Peso;
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }
                else col.Visible = false;
            }
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi) Dialogos.Error(this, ex);
        }
        finally
        {
            _cargandoTop = false;
            if (PuedeActualizarUi) ActualizarBotones();
            Tema.Cargando(_grdTopSql, false);
        }
    }

    /// <summary>Copia el SQL completo de la fila elegida al editor y vuelve a la pestaña Plan.</summary>
    private async Task AnalizarAsync()
    {
        if (_analizando || _grdTopSql.CurrentRow?.DataBoundItem is not DataRowView fila || fila["SQL_ID"] is not string sqlId) return;
        try
        {
            _analizando = true;
            ActualizarBotones();
            UseWaitCursor = true;
            var sql = await ServicioPlanes.TextoCompletoAsync(Bd, sqlId);
            if (!PuedeActualizarUi) return;
            if (sql is null) throw new InvalidOperationException("Esa sentencia ya no está en la caché de Oracle. Refresque la lista.");
            try { ServicioPlanes.Validar(sql); }   // un DML del top no se puede explicar desde aquí
            catch (ArgumentException) { throw new ArgumentException("Esa sentencia no es un SELECT ni un WITH, así que no se puede analizar aquí."); }
            _txtConsulta.Text = sql.Trim();
            _subpestanas.Seleccionar(0);
            _txtConsulta.Focus();
            _txtConsulta.SelectionStart = _txtConsulta.TextLength;
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi) Dialogos.Error(this, ex);
        }
        finally
        {
            _analizando = false;
            if (PuedeActualizarUi)
            {
                UseWaitCursor = false;
                ActualizarBotones();
            }
        }
    }

    // ------------------------------------------------------------------ varios

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario);
        _btnEstimado.Enabled = _btnReal.Enabled = conSesion && !_ejecutando;
        _btnAntes.Enabled = _ultimoEstimado is not null && !_ejecutando;
        _btnQuitarAntes.Enabled = _antes is not null;
        _btnRefrescar.Enabled = conSesion && !_cargandoTop;
        _btnAnalizar.Enabled = !_analizando && _grdTopSql.CurrentRow?.DataBoundItem is DataRowView;
    }

    private static Tarjeta Crear(string titulo, int ancho) =>
        new() { Titulo = titulo, Size = new Size(ancho, 100), Margin = new Padding(0, 0, 12, 0) };
}

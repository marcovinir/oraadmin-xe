using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Dashboard;
using OraAdminXE.UI;
using ScottPlot.WinForms;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Pestaña de métricas en vivo con tarjetas, gráfico ScottPlot e historial SNAPSHOT (issue #24).
/// </summary>
public sealed class UcMetricas : UserControl
{
    private static IBaseDatos Bd => Sesion.Bd;

    private readonly TableLayoutPanel _tlpTarjetas;
    private readonly Tarjeta _tarSesiones;
    private readonly Tarjeta _tarCpu;
    private readonly Tarjeta _tarLecturas;
    private readonly Tarjeta _tarEjecuciones;
    private readonly Tarjeta _tarTransacciones;

    private readonly ComboBox _cboIntervalo;
    private readonly ComboBox _cboMetrica;
    private readonly BotonPlano _btnIniciar;
    private readonly BotonPlano _btnDetener;
    private readonly BotonPlano _btnBorrarHistorial;
    private readonly BotonPlano _btnRefrescar;
    private readonly Label _lblEstado;

    private readonly FormsPlot _formsPlot;
    private readonly System.Windows.Forms.Timer _timer;

    private bool _monitoreoActivo;
    private bool _muestraEnCurso;
    private readonly List<MetricaItem> _metricasDisponibles = [];

    public UcMetricas()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Fondo;

        // Barra de acciones superior
        var pnlAcciones = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        var lblIntervalo = new Label
        {
            Text = "Muestreo:",
            ForeColor = Tema.TextoSecundario,
            AutoSize = true,
            Margin = new Padding(0, 8, 4, 0)
        };

        _cboIntervalo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 80,
            Margin = new Padding(0, 4, 12, 0)
        };
        _cboIntervalo.Items.AddRange(["5 s", "15 s", "30 s", "60 s"]);
        _cboIntervalo.SelectedIndex = 1; // 15 s por defecto
        _cboIntervalo.SelectedIndexChanged += (_, _) => ActualizarIntervaloTimer();

        _btnIniciar = Tema.Boton("Iniciar monitoreo", primario: true, icono: "");
        _btnIniciar.Click += async (_, _) => await IniciarMonitoreoAsync();

        _btnDetener = Tema.Boton("Detener monitoreo", primario: false, icono: "");
        _btnDetener.Enabled = false;
        _btnDetener.Click += async (_, _) => await DetenerMonitoreoAsync();

        _btnRefrescar = Tema.Boton("Refrescar", primario: false, icono: "");
        _btnRefrescar.Click += async (_, _) => await RefrescarTodoAsync();

        _btnBorrarHistorial = new BotonPlano
        {
            Text = "Borrar historial",
            Estilo = EstiloBoton.Peligro,
            Icono = "",
            Margin = new Padding(12, 3, 3, 3)
        };
        _btnBorrarHistorial.Click += async (_, _) => await BorrarHistorialAsync();

        _lblEstado = new Label
        {
            Text = "Monitoreo detenido",
            ForeColor = Tema.TextoSecundario,
            AutoSize = true,
            Margin = new Padding(16, 8, 0, 0)
        };

        pnlAcciones.Controls.AddRange([
            lblIntervalo, _cboIntervalo,
            _btnIniciar, _btnDetener, _btnRefrescar, _btnBorrarHistorial,
            _lblEstado
        ]);

        // Panel de 5 tarjetas
        _tlpTarjetas = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 110,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 10)
        };
        for (int i = 0; i < 5; i++)
            _tlpTarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        _tlpTarjetas.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _tarSesiones = new Tarjeta { Titulo = "Sesiones Activas", Valor = "-", Detalle = "sesiones", Acento = Tema.Info };
        _tarCpu = new Tarjeta { Titulo = "CPU", Valor = "-", Detalle = "cs/s", Acento = Tema.Advertencia };
        _tarLecturas = new Tarjeta { Titulo = "Lecturas Lógicas", Valor = "-", Detalle = "lect/s", Acento = Tema.Acento };
        _tarEjecuciones = new Tarjeta { Titulo = "Ejecuciones", Valor = "-", Detalle = "ejec/s", Acento = Tema.Ok };
        _tarTransacciones = new Tarjeta { Titulo = "Transacciones", Valor = "-", Detalle = "tx/s", Acento = Tema.AcentoHover };

        _tlpTarjetas.Controls.Add(_tarSesiones, 0, 0);
        _tlpTarjetas.Controls.Add(_tarCpu, 1, 0);
        _tlpTarjetas.Controls.Add(_tarLecturas, 2, 0);
        _tlpTarjetas.Controls.Add(_tarEjecuciones, 3, 0);
        _tlpTarjetas.Controls.Add(_tarTransacciones, 4, 0);

        // Panel de Gráfico
        var pnlGraficoHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        var lblMetrica = new Label
        {
            Text = "Métrica a graficar:",
            ForeColor = Tema.Texto,
            Font = Tema.FuenteNegrita,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        };

        _cboMetrica = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 260,
            Margin = new Padding(0, 2, 16, 0)
        };
        _cboMetrica.SelectedIndexChanged += async (_, _) => await CargarGraficoAsync();

        var lblHistorialInfo = new Label
        {
            Text = "(Última hora de muestras en SNAPSHOT)",
            ForeColor = Tema.TextoSecundario,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };

        pnlGraficoHeader.Controls.AddRange([lblMetrica, _cboMetrica, lblHistorialInfo]);

        _formsPlot = new FormsPlot
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };

        var pnlGraficoContenedor = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 6, 0, 0)
        };
        pnlGraficoContenedor.Controls.Add(_formsPlot);
        pnlGraficoContenedor.Controls.Add(pnlGraficoHeader);

        Controls.Add(pnlGraficoContenedor);
        Controls.Add(_tlpTarjetas);
        Controls.Add(pnlAcciones);

        // Temporizador de muestreo
        _timer = new System.Windows.Forms.Timer { Interval = 15000 };
        _timer.Tick += async (_, _) => await TomarMuestraYRefrescarAsync();
    }

    public void AlActivar()
    {
        _ = RefrescarTodoAsync();
    }

    public void AlDesactivar()
    {
        // Se detiene el temporizador al salir del módulo
        if (_monitoreoActivo)
        {
            _timer.Stop();
            _monitoreoActivo = false;
            _btnIniciar.Enabled = true;
            _btnDetener.Enabled = false;
            _lblEstado.Text = "Monitoreo detenido";
            _lblEstado.ForeColor = Tema.TextoSecundario;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }

    private int ObtenerSegundosIntervalo()
    {
        var texto = _cboIntervalo.SelectedItem?.ToString() ?? "15 s";
        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        return int.TryParse(digitos, out var s) ? ServicioMetricas.ValidarIntervalo(s) : 15;
    }

    private void ActualizarIntervaloTimer()
    {
        var seg = ObtenerSegundosIntervalo();
        _timer.Interval = seg * 1000;
        if (_monitoreoActivo)
        {
            _lblEstado.Text = $"Monitoreo activo (cada {seg} s)";
        }
    }

    private async Task IniciarMonitoreoAsync()
    {
        var seg = ObtenerSegundosIntervalo();
        _timer.Interval = seg * 1000;
        _timer.Start();
        _monitoreoActivo = true;

        _btnIniciar.Enabled = false;
        _btnDetener.Enabled = true;
        _lblEstado.Text = $"Monitoreo activo (cada {seg} s)";
        _lblEstado.ForeColor = Tema.Ok;

        try
        {
            await Bd.RegistrarBitacoraAsync(Modulo.Performance, "Iniciar monitoreo de métricas", null, true, $"Intervalo: {seg} s");
        }
        catch { /* La bitácora no interrumpe */ }

        // Toma una muestra inmediata
        if (_monitoreoActivo && !IsDisposed) await TomarMuestraYRefrescarAsync();
    }

    private async Task DetenerMonitoreoAsync()
    {
        _timer.Stop();
        _monitoreoActivo = false;

        _btnIniciar.Enabled = true;
        _btnDetener.Enabled = false;
        _lblEstado.Text = "Monitoreo detenido";
        _lblEstado.ForeColor = Tema.TextoSecundario;

        try
        {
            await Bd.RegistrarBitacoraAsync(Modulo.Performance, "Detener monitoreo de métricas", null, true, null);
        }
        catch { /* La bitácora no interrumpe */ }
    }

    private async Task TomarMuestraYRefrescarAsync()
    {
        if (!_monitoreoActivo || _muestraEnCurso || IsDisposed) return;
        _muestraEnCurso = true;
        try
        {
            await ServicioMetricas.RegistrarMuestraAsync(Bd);
            if (!_monitoreoActivo || IsDisposed) return;
            _lblEstado.Text = $"Monitoreo activo (cada {ObtenerSegundosIntervalo()} s) — Muestra: {DateTime.Now:HH:mm:ss}";
            _lblEstado.ForeColor = Tema.Ok;
            await RefrescarTodoAsync();
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            _lblEstado.Text = $"Error al registrar muestra: {ex.Message}";
            _lblEstado.ForeColor = Tema.Peligro;
        }
        finally { _muestraEnCurso = false; }
    }

    private async Task RefrescarTodoAsync()
    {
        await CargarMetricasActualesAsync();
        await CargarGraficoAsync();
    }

    private async Task CargarMetricasActualesAsync()
    {
        try
        {
            var dt = await Bd.ConsultarAsync(SqlPerformance.MetricasActuales);
            var metricas = ServicioMetricas.ParsearMetricas(dt);

            _metricasDisponibles.Clear();
            _metricasDisponibles.AddRange(metricas);

            // Actualizar tarjetas
            foreach (var m in metricas)
            {
                var valorFormateado = m.Valor.ToString("N2");
                switch (m.Nombre)
                {
                    case "SESIONES_ACTIVAS":
                        _tarSesiones.Valor = valorFormateado;
                        _tarSesiones.Detalle = m.Unidad;
                        break;
                    case "CPU_POR_SEG":
                        _tarCpu.Valor = valorFormateado;
                        _tarCpu.Detalle = m.Unidad;
                        break;
                    case "LECTURAS_LOGICAS":
                        _tarLecturas.Valor = valorFormateado;
                        _tarLecturas.Detalle = m.Unidad;
                        break;
                    case "EJECUCIONES_SEG":
                        _tarEjecuciones.Valor = valorFormateado;
                        _tarEjecuciones.Detalle = m.Unidad;
                        break;
                    case "TRANSACCIONES_SEG":
                        _tarTransacciones.Valor = valorFormateado;
                        _tarTransacciones.Detalle = m.Unidad;
                        break;
                }
            }

            // Sincronizar ComboBox si aún no tiene elementos
            if (_cboMetrica.Items.Count == 0 && metricas.Count > 0)
            {
                foreach (var m in metricas)
                    _cboMetrica.Items.Add(new MetricaComboItem(m.IdMetrica, ServicioMetricas.FormatearNombre(m.Nombre), m.Unidad));

                _cboMetrica.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _lblEstado.Text = $"Error al leer métricas: {ex.Message}";
            _lblEstado.ForeColor = Tema.Peligro;
        }
    }

    private async Task CargarGraficoAsync()
    {
        if (_cboMetrica.SelectedItem is not MetricaComboItem seleccionado) return;

        try
        {
            var pId = new OracleParameter("m", seleccionado.IdMetrica);
            var dt = await Bd.ConsultarAsync(SqlPerformance.HistorialMetrica, pId);
            var puntos = ServicioMetricas.ParsearHistorial(dt);

            _formsPlot.Plot.Clear();
            _formsPlot.Plot.Axes.Color(new ScottPlot.Color(Tema.Texto.R, Tema.Texto.G, Tema.Texto.B));

            if (puntos.Count > 0)
            {
                var (xs, ys) = ServicioMetricas.PrepararDatosGrafico(puntos);
                var scatter = _formsPlot.Plot.Add.Scatter(xs, ys);
                scatter.Color = new ScottPlot.Color(Tema.Acento.R, Tema.Acento.G, Tema.Acento.B);
                scatter.LineWidth = 2;
                scatter.MarkerSize = 5;

                _formsPlot.Plot.Axes.DateTimeTicksBottom();
                _formsPlot.Plot.Title($"{seleccionado.Nombre} ({seleccionado.Unidad})");
                _formsPlot.Plot.Axes.Left.Label.Text = seleccionado.Unidad;
                _formsPlot.Plot.Axes.Bottom.Label.Text = "Hora";
                _formsPlot.Plot.FigureBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);
                _formsPlot.Plot.DataBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);
                _formsPlot.Plot.Axes.AutoScale();
            }
            else
            {
                _formsPlot.Plot.Title($"{seleccionado.Nombre} ({seleccionado.Unidad}) — Sin datos en la última hora");
                _formsPlot.Plot.FigureBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);
                _formsPlot.Plot.DataBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);
            }

            _formsPlot.Refresh();
        }
        catch (Exception ex)
        {
            _lblEstado.Text = $"Error al cargar gráfico: {ex.Message}";
            _lblEstado.ForeColor = Tema.Peligro;
        }
    }

    private async Task BorrarHistorialAsync()
    {
        if (!Dialogos.ConfirmarSql(this, "Borrar historial de métricas", SqlPerformance.BorrarHistorial + ";", destructiva: true))
            return;

        try
        {
            await ServicioMetricas.BorrarHistorialAsync(Bd);
            await CargarGraficoAsync();
            _lblEstado.Text = "Historial borrado correctamente.";
            _lblEstado.ForeColor = Tema.Ok;
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
    }

    private sealed record MetricaComboItem(int IdMetrica, string Nombre, string Unidad)
    {
        public override string ToString() => $"{Nombre} ({Unidad})";
    }
}

using System.Data;
using Label = System.Windows.Forms.Label;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Dashboard;
using OraAdminXE.UI;
using ScottPlot;
using ScottPlot.TickGenerators;
using ScottPlot.WinForms;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Pestaña de Esperas y memoria en Performance: eventos de espera, top SQL,
/// memoria SGA/PGA y operaciones largas en curso (issue #25).
/// </summary>
public sealed class UcEsperasMemoria : UserControl
{
    private static IBaseDatos Bd => Sesion.Bd;

    private readonly BotonPlano _btnRefrescar;
    private readonly Label _lblEstado;
    private readonly Pestanas _subPestanas;
    private bool _refrescando;

    // Sub-pestaña 1: Esperas
    private readonly DataGridView _grdEventos;
    private readonly FormsPlot _plotEventos;

    // Sub-pestaña 2: Top SQL
    private readonly ComboBox _cboCriterioTopSql;
    private readonly BotonPlano _btnVerSql;
    private readonly DataGridView _grdTopSql;

    // Sub-pestaña 3: Memoria
    private readonly Tarjeta _tarPgaAllocated;
    private readonly Tarjeta _tarPgaInUse;
    private readonly Tarjeta _tarPgaMax;
    private readonly DataGridView _grdSga;
    private readonly FormsPlot _plotSga;

    // Sub-pestaña 4: Operaciones largas
    private readonly DataGridView _grdLongOps;

    public UcEsperasMemoria()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Fondo;

        // Barra superior
        var pnlSuperior = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        _btnRefrescar = Tema.Boton("Refrescar", primario: true, icono: "");
        _btnRefrescar.Click += async (_, _) => await RefrescarTodoAsync();

        _lblEstado = new Label
        {
            Text = "Cargando datos de rendimiento...",
            ForeColor = Tema.TextoSecundario,
            AutoSize = true,
            Margin = new Padding(12, 8, 0, 0)
        };

        pnlSuperior.Controls.AddRange([_btnRefrescar, _lblEstado]);

        _subPestanas = new Pestanas { Dock = DockStyle.Fill };

        // 1. Panel de Eventos de espera
        _grdEventos = CrearGrilla();
        _grdEventos.Columns.Add("EVENT", "Evento de espera");
        _grdEventos.Columns.Add("WAIT_CLASS", "Clase");
        _grdEventos.Columns.Add("TOTAL_WAITS", "Total esperas");
        _grdEventos.Columns.Add("SEG", "Tiempo (seg)");
        _grdEventos.Columns["EVENT"]!.FillWeight = 40;
        _grdEventos.Columns["WAIT_CLASS"]!.FillWeight = 25;
        _grdEventos.Columns["TOTAL_WAITS"]!.FillWeight = 15;
        _grdEventos.Columns["SEG"]!.FillWeight = 20;

        _plotEventos = new FormsPlot { Dock = DockStyle.Fill };

        var splitEventos = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = System.Windows.Forms.Orientation.Vertical,
            SplitterDistance = 70
        };
        splitEventos.Panel1.Controls.Add(_grdEventos);
        splitEventos.Panel2.Controls.Add(_plotEventos);

        // 2. Panel de Top SQL
        var pnlTopSqlBarra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        var lblCriterio = new Label
        {
            Text = "Ordenar por:",
            ForeColor = Tema.Texto,
            Font = Tema.FuenteNegrita,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        };

        _cboCriterioTopSql = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240,
            Margin = new Padding(0, 2, 12, 0)
        };
        foreach (var c in ServicioEsperasMemoria.CriteriosTopSql)
            _cboCriterioTopSql.Items.Add(new CriterioComboItem(c.Codigo, c.Descripcion));
        _cboCriterioTopSql.SelectedIndex = 0;
        _cboCriterioTopSql.SelectedIndexChanged += async (_, _) => await RefrescarTodoAsync();

        _btnVerSql = Tema.Boton("Ver SQL completo", primario: false, icono: "");
        _btnVerSql.Click += async (_, _) => await VerSqlCompletoAsync();

        pnlTopSqlBarra.Controls.AddRange([lblCriterio, _cboCriterioTopSql, _btnVerSql]);

        _grdTopSql = CrearGrilla();
        _grdTopSql.Columns.Add("SQL_ID", "SQL ID");
        _grdTopSql.Columns.Add("EXECUTIONS", "Ejecuciones");
        _grdTopSql.Columns.Add("SEG", "Tiempo (seg)");
        _grdTopSql.Columns.Add("CPU_SEG", "CPU (seg)");
        _grdTopSql.Columns.Add("BUFFER_GETS", "Lecturas búfer");
        _grdTopSql.Columns.Add("DISK_READS", "Lecturas disco");
        _grdTopSql.Columns.Add("TEXTO", "Sentencia SQL");
        _grdTopSql.Columns["SQL_ID"]!.FillWeight = 12;
        _grdTopSql.Columns["EXECUTIONS"]!.FillWeight = 10;
        _grdTopSql.Columns["SEG"]!.FillWeight = 10;
        _grdTopSql.Columns["CPU_SEG"]!.FillWeight = 10;
        _grdTopSql.Columns["BUFFER_GETS"]!.FillWeight = 12;
        _grdTopSql.Columns["DISK_READS"]!.FillWeight = 12;
        _grdTopSql.Columns["TEXTO"]!.FillWeight = 34;
        _grdTopSql.CellDoubleClick += async (_, _) => await VerSqlCompletoAsync();

        var pnlTopSqlContenedor = new Panel { Dock = DockStyle.Fill };
        pnlTopSqlContenedor.Controls.Add(_grdTopSql);
        pnlTopSqlContenedor.Controls.Add(pnlTopSqlBarra);

        // 3. Panel de Memoria
        var pnlMemoriaSuperior = new Panel { Dock = DockStyle.Top, Height = 140 };

        var tlpPga = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 4)
        };
        tlpPga.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tlpPga.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tlpPga.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        tlpPga.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _tarPgaAllocated = new Tarjeta { Titulo = "PGA Total Asignada", Valor = "-", Detalle = "v$pgastat", Acento = Tema.Info };
        _tarPgaInUse = new Tarjeta { Titulo = "PGA En Uso", Valor = "-", Detalle = "v$pgastat", Acento = Tema.Ok };
        _tarPgaMax = new Tarjeta { Titulo = "PGA Máxima Asignada", Valor = "-", Detalle = "v$pgastat", Acento = Tema.Advertencia };

        tlpPga.Controls.Add(_tarPgaAllocated, 0, 0);
        tlpPga.Controls.Add(_tarPgaInUse, 1, 0);
        tlpPga.Controls.Add(_tarPgaMax, 2, 0);

        var lblLimiteXe = new Label
        {
            Text = "ℹ En Oracle Database XE 21c la memoria RAM total direccionable está limitada a 2 GB (SGA + PGA).",
            ForeColor = Tema.Advertencia,
            Font = Tema.FuentePequena,
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };

        pnlMemoriaSuperior.Controls.Add(tlpPga);
        pnlMemoriaSuperior.Controls.Add(lblLimiteXe);

        _grdSga = CrearGrilla();
        _grdSga.Columns.Add("NAME", "Componente SGA");
        _grdSga.Columns.Add("MB", "Tamaño (MB)");
        _grdSga.Columns.Add("RESIZEABLE", "Redimensionable");
        _grdSga.Columns["NAME"]!.FillWeight = 50;
        _grdSga.Columns["MB"]!.FillWeight = 25;
        _grdSga.Columns["RESIZEABLE"]!.FillWeight = 25;

        _plotSga = new FormsPlot { Dock = DockStyle.Fill };

        var splitSga = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = System.Windows.Forms.Orientation.Vertical,
            SplitterDistance = 70
        };
        splitSga.Panel1.Controls.Add(_grdSga);
        splitSga.Panel2.Controls.Add(_plotSga);

        var pnlMemoriaContenedor = new Panel { Dock = DockStyle.Fill };
        pnlMemoriaContenedor.Controls.Add(splitSga);
        pnlMemoriaContenedor.Controls.Add(pnlMemoriaSuperior);

        // 4. Panel de Operaciones largas
        var pnlLongOpsHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
        var lblLongOpsInfo = new Label
        {
            Text = "Operaciones largas activas (v$session_longops). Monitorea respaldos Data Pump, reconstrucciones de índices y consultas masivas.",
            ForeColor = Tema.TextoSecundario,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlLongOpsHeader.Controls.Add(lblLongOpsInfo);

        _grdLongOps = CrearGrilla();
        _grdLongOps.Columns.Add("SID", "SID");
        _grdLongOps.Columns.Add("OPNAME", "Operación");
        _grdLongOps.Columns.Add("TARGET", "Destino");
        _grdLongOps.Columns.Add("SOFAR", "Procesado");
        _grdLongOps.Columns.Add("TOTALWORK", "Total");
        _grdLongOps.Columns.Add("PCT", "% Avance");
        _grdLongOps.Columns.Add("TIME_REMAINING", "Restante (seg)");
        _grdLongOps.Columns["SID"]!.FillWeight = 8;
        _grdLongOps.Columns["OPNAME"]!.FillWeight = 25;
        _grdLongOps.Columns["TARGET"]!.FillWeight = 20;
        _grdLongOps.Columns["SOFAR"]!.FillWeight = 12;
        _grdLongOps.Columns["TOTALWORK"]!.FillWeight = 12;
        _grdLongOps.Columns["PCT"]!.FillWeight = 11;
        _grdLongOps.Columns["TIME_REMAINING"]!.FillWeight = 12;

        var pnlLongOpsContenedor = new Panel { Dock = DockStyle.Fill };
        pnlLongOpsContenedor.Controls.Add(_grdLongOps);
        pnlLongOpsContenedor.Controls.Add(pnlLongOpsHeader);

        // Ensamblar sub-pestañas
        _subPestanas.Agregar("Eventos de espera", splitEventos);
        _subPestanas.Agregar("Top SQL", pnlTopSqlContenedor);
        _subPestanas.Agregar("Memoria SGA / PGA", pnlMemoriaContenedor);
        _subPestanas.Agregar("Operaciones largas", pnlLongOpsContenedor);

        Controls.Add(_subPestanas);
        Controls.Add(pnlSuperior);
    }

    public void AlActivar()
    {
        _ = RefrescarTodoAsync();
    }

    private static DataGridView CrearGrilla()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };
        Tema.AplicarGrilla(g);
        return g;
    }

    private async Task RefrescarTodoAsync()
    {
        if (_refrescando) return;
        _refrescando = true;
        _btnRefrescar.Enabled = false;
        _cboCriterioTopSql.Enabled = false;
        _lblEstado.Text = "Refrescando datos...";
        _lblEstado.ForeColor = Tema.TextoSecundario;

        try
        {
            await Task.WhenAll(
                CargarEventosEsperaAsync(),
                CargarTopSqlAsync(),
                CargarMemoriaAsync(),
                CargarOperacionesLargasAsync()
            );

            _lblEstado.Text = $"Actualizado a las {DateTime.Now:HH:mm:ss}";
            _lblEstado.ForeColor = Tema.Ok;
        }
        catch (Exception ex)
        {
            _lblEstado.Text = $"Error al refrescar: {ex.Message}";
            _lblEstado.ForeColor = Tema.Peligro;
        }
        finally
        {
            _refrescando = false;
            _btnRefrescar.Enabled = true;
            _cboCriterioTopSql.Enabled = true;
        }
    }

    private async Task CargarEventosEsperaAsync()
    {
        try
        {
            var dt = await Bd.ConsultarAsync(SqlPerformance.EventosEspera);
            var eventos = ServicioEsperasMemoria.ParsearEventos(dt);

            _grdEventos.Rows.Clear();
            foreach (var e in eventos)
            {
                _grdEventos.Rows.Add(e.Evento, e.Clase, e.TotalWaits.ToString("N0"), e.Segundos.ToString("N2"));
            }

            if (eventos.Count == 0)
                Tema.MensajeVacio(_grdEventos, "No hay eventos de espera significativos.", "");

            // ScottPlot horizontal bar chart
            _plotEventos.Plot.Clear();
            _plotEventos.Plot.Axes.Color(new ScottPlot.Color(Tema.Texto.R, Tema.Texto.G, Tema.Texto.B));
            _plotEventos.Plot.FigureBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);
            _plotEventos.Plot.DataBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);

            if (eventos.Count > 0)
            {
                // Invertir para que el mayor quede arriba
                var topEventos = eventos.Take(10).Reverse().ToList();
                var bars = new Bar[topEventos.Count];
                var ticks = new Tick[topEventos.Count];

                for (int i = 0; i < topEventos.Count; i++)
                {
                    bars[i] = new Bar
                    {
                        Position = i + 1,
                        Value = topEventos[i].Segundos,
                        FillColor = new ScottPlot.Color(Tema.Acento.R, Tema.Acento.G, Tema.Acento.B)
                    };
                    var nombreCorto = topEventos[i].Evento.Length > 24
                        ? topEventos[i].Evento[..21] + "..."
                        : topEventos[i].Evento;
                    ticks[i] = new Tick(i + 1, nombreCorto);
                }

                var barPlot = _plotEventos.Plot.Add.Bars(bars);
                barPlot.Horizontal = true;
                _plotEventos.Plot.Axes.Left.TickGenerator = new NumericManual(ticks);
                _plotEventos.Plot.Axes.Bottom.Label.Text = "Segundos acumulados";
                _plotEventos.Plot.Title("Top 10 Eventos de Espera (seg)");
                _plotEventos.Plot.Axes.AutoScale();
            }
            else
            {
                _plotEventos.Plot.Title("Sin eventos de espera activos");
            }

            _plotEventos.Refresh();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al cargar esperas: {ex.Message}", ex);
        }
    }

    private async Task CargarTopSqlAsync()
    {
        try
        {
            var criterio = (_cboCriterioTopSql.SelectedItem as CriterioComboItem)?.Codigo ?? "elapsed_time";
            var sql = SqlPerformance.SqlTopSql(criterio);
            var dt = await Bd.ConsultarAsync(sql);
            var items = ServicioEsperasMemoria.ParsearTopSql(dt);

            _grdTopSql.Rows.Clear();
            foreach (var s in items)
            {
                _grdTopSql.Rows.Add(
                    s.SqlId,
                    s.Ejecuciones.ToString("N0"),
                    s.Segundos.ToString("N2"),
                    s.CpuSegundos.ToString("N2"),
                    s.BufferGets.ToString("N0"),
                    s.DiskReads.ToString("N0"),
                    s.Texto
                );
            }

            if (items.Count == 0)
                Tema.MensajeVacio(_grdTopSql, "No se encontraron consultas registradas en v$sqlarea.", "");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al cargar Top SQL: {ex.Message}", ex);
        }
    }

    private async Task VerSqlCompletoAsync()
    {
        if (_grdTopSql.CurrentRow == null) return;
        var sqlId = _grdTopSql.CurrentRow.Cells["SQL_ID"].Value?.ToString();
        if (string.IsNullOrEmpty(sqlId)) return;

        try
        {
            var pId = new OracleParameter("id", sqlId);
            var dt = await Bd.ConsultarAsync(SqlPerformance.SqlDeSesion, pId);
            var sqlCompleto = dt.Rows.Count > 0 ? dt.Rows[0][0]?.ToString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(sqlCompleto))
                sqlCompleto = _grdTopSql.CurrentRow.Cells["TEXTO"].Value?.ToString() ?? "";

            using var dlg = new DlgSql(DlgSql.Modo.Ver, $"SQL {sqlId}", sqlCompleto);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
    }

    private async Task CargarMemoriaAsync()
    {
        try
        {
            // PGA
            var dtPga = await Bd.ConsultarAsync(SqlPerformance.PgaStat);
            var pga = ServicioEsperasMemoria.ParsearPga(dtPga);

            _tarPgaAllocated.Valor = $"{(pga.TryGetValue("total PGA allocated", out var alloc) ? alloc : 0)} MB";
            _tarPgaInUse.Valor = $"{(pga.TryGetValue("total PGA inuse", out var inuse) ? inuse : 0)} MB";
            _tarPgaMax.Valor = $"{(pga.TryGetValue("maximum PGA allocated", out var maxpga) ? maxpga : 0)} MB";

            // SGA
            var dtSga = await Bd.ConsultarAsync(SqlPerformance.SgaInfo);
            var componentes = ServicioEsperasMemoria.ParsearSga(dtSga);

            _grdSga.Rows.Clear();
            foreach (var c in componentes)
            {
                _grdSga.Rows.Add(c.Nombre, c.Mb.ToString("N0"), c.Redimensionable ?? "-");
            }

            // Pie Chart SGA
            _plotSga.Plot.Clear();
            _plotSga.Plot.Axes.Color(new ScottPlot.Color(Tema.Texto.R, Tema.Texto.G, Tema.Texto.B));
            _plotSga.Plot.FigureBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);
            _plotSga.Plot.DataBackground.Color = new ScottPlot.Color(Tema.Superficie.R, Tema.Superficie.G, Tema.Superficie.B);

            var paraGrafico = ServicioEsperasMemoria.FiltrarComponentesParaGrafico(componentes);
            if (paraGrafico.Count > 0)
            {
                ScottPlot.Color[] colores =
                [
                    new(Tema.Acento.R, Tema.Acento.G, Tema.Acento.B),
                    new(Tema.Info.R, Tema.Info.G, Tema.Info.B),
                    new(Tema.Ok.R, Tema.Ok.G, Tema.Ok.B),
                    new(Tema.Advertencia.R, Tema.Advertencia.G, Tema.Advertencia.B),
                    new(Tema.AcentoHover.R, Tema.AcentoHover.G, Tema.AcentoHover.B),
                    new(Tema.Peligro.R, Tema.Peligro.G, Tema.Peligro.B),
                    new(148, 163, 184),
                    new(203, 213, 225)
                ];

                var slices = new PieSlice[paraGrafico.Count];
                for (int i = 0; i < paraGrafico.Count; i++)
                {
                    slices[i] = new PieSlice
                    {
                        Value = paraGrafico[i].Mb,
                        FillColor = colores[i % colores.Length],
                        Label = $"{paraGrafico[i].Nombre} ({paraGrafico[i].Mb} MB)"
                    };
                }

                _plotSga.Plot.Add.Pie(slices);
                _plotSga.Plot.ShowLegend();
                _plotSga.Plot.Axes.Frameless();
                _plotSga.Plot.HideGrid();
                _plotSga.Plot.Title("Distribución de Componentes SGA (MB)");
            }
            else
            {
                _plotSga.Plot.Title("Sin datos disponibles de SGA");
            }

            _plotSga.Refresh();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al cargar memoria: {ex.Message}", ex);
        }
    }

    private async Task CargarOperacionesLargasAsync()
    {
        try
        {
            var dt = await Bd.ConsultarAsync(SqlPerformance.OperacionesLargas);
            var ops = ServicioEsperasMemoria.ParsearOperacionesLargas(dt);

            _grdLongOps.Rows.Clear();
            foreach (var o in ops)
            {
                _grdLongOps.Rows.Add(
                    o.Sid,
                    o.Operacion,
                    o.Destino,
                    o.SoFar.ToString("N0"),
                    o.TotalWork.ToString("N0"),
                    $"{o.Porcentaje:N0}%",
                    o.TiempoRestante.ToString("N0")
                );
            }

            if (ops.Count == 0)
                Tema.MensajeVacio(_grdLongOps, "No hay operaciones largas en curso en este momento.", "");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al cargar operaciones largas: {ex.Message}", ex);
        }
    }

    private sealed record CriterioComboItem(string Codigo, string Descripcion)
    {
        public override string ToString() => Descripcion;
    }
}

using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Pestaña Sesiones y bloqueos (issue #23, P4):
/// - Monitoreo de sesiones de usuario con refresco automático cada 5 s.
/// - Detección visual de sesiones bloqueadas (amarillo) y bloqueadoras (rojo).
/// - Panel de bloqueos cruzados con v$locked_object.
/// - Visualización de SQL activo y terminación de sesiones (ALTER SYSTEM KILL SESSION).
/// - Protección para evitar matar las sesiones de la propia aplicación.
/// </summary>
public sealed class UcSesiones : UserControl
{
    private readonly DataGridView _grdSesiones = new() { Dock = DockStyle.Fill };
    private readonly BotonPlano _btnMatar = Tema.Boton("Matar sesión…", icono: "\uE74D");
    private readonly BotonPlano _btnVerSql = Tema.Boton("Ver SQL…", icono: "\uE721");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "\uE72C");
    private readonly CheckBox _chkPausar = new()
    {
        Text = "Pausar refresco automático (5 s)",
        AutoSize = true,
        Margin = new Padding(12, 12, 0, 0),
        ForeColor = Tema.TextoSecundario
    };
    private readonly Label _lblEstado = new()
    {
        AutoSize = true,
        Margin = new Padding(8, 14, 0, 0),
        ForeColor = Tema.TextoSecundario,
        Font = Tema.FuentePequena
    };

    private readonly Label _lblTituloBloqueos = new()
    {
        Text = "Bloqueos activos entre sesiones",
        Dock = DockStyle.Top,
        Height = 28,
        Font = Tema.FuenteNegrita,
        ForeColor = Tema.TextoSecundario
    };
    private readonly TextBox _txtBloqueos = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Tema.Superficie,
        ForeColor = Tema.Texto,
        BorderStyle = BorderStyle.None,
        Font = Tema.Fuente
    };
    private readonly Panel _pnlBloqueos = new()
    {
        Dock = DockStyle.Bottom,
        Height = 130,
        Padding = new Padding(0, 8, 0, 0)
    };

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };
    private bool _cargando;
    private int _version;
    private HashSet<int> _sidsBloqueadores = [];

    private static IBaseDatos Bd => Sesion.Bd;

    public UcSesiones()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnMatar.Estilo = EstiloBoton.Peligro;

        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        barra.Controls.AddRange([_btnMatar, _btnVerSql, _btnRefrescar, _chkPausar, _lblEstado]);

        _pnlBloqueos.Controls.Add(_txtBloqueos);
        _pnlBloqueos.Controls.Add(_lblTituloBloqueos);

        Controls.Add(_grdSesiones);
        Controls.Add(_pnlBloqueos);
        Controls.Add(barra);

        Tema.AplicarGrilla(_grdSesiones);
        Tema.MensajeVacio(_grdSesiones, "No hay sesiones de usuario activas", "\uE9D9");
        _grdSesiones.MultiSelect = false;

        Tema.ColumnaEstado(_grdSesiones, "STATUS");
        Tema.ColumnaEstado(_grdSesiones, "ES_LA_APP", v => v == "SI" ? TipoEstado.Info : TipoEstado.Neutro);

        _grdSesiones.CellFormatting += GrdSesiones_CellFormatting;
        _grdSesiones.SelectionChanged += (_, _) => ActualizarBotones();

        _btnMatar.Click += async (_, _) => await MatarSesionSeleccionadaAsync();
        _btnVerSql.Click += async (_, _) => await VerSqlActivoAsync();
        _btnRefrescar.Click += async (_, _) => await CargarAsync(esAutoRefresco: false);

        _chkPausar.CheckedChanged += (_, _) =>
        {
            if (_chkPausar.Checked)
            {
                _timer.Stop();
                _lblEstado.Text = "Refresco pausado";
                _lblEstado.ForeColor = Tema.Advertencia;
            }
            else
            {
                _timer.Start();
                _lblEstado.Text = "Refresco activo (5 s)";
                _lblEstado.ForeColor = Tema.TextoSecundario;
            }
        };

        _timer.Tick += async (_, _) => await CargarAsync(esAutoRefresco: true);
        ActualizarBotones();
    }

    private bool PuedeActualizarUi => !IsDisposed && !Disposing;

    private DataRow? FilaSeleccionada =>
        (_grdSesiones.CurrentRow?.DataBoundItem as DataRowView)?.Row;

    /// <summary>Se llama al entrar al módulo Performance.</summary>
    public void AlActivar()
    {
        if (!_chkPausar.Checked)
        {
            _timer.Start();
            _lblEstado.Text = "Refresco activo (5 s)";
            _lblEstado.ForeColor = Tema.TextoSecundario;
        }
        _ = CargarAsync(esAutoRefresco: false);
    }

    /// <summary>Se llama al salir del módulo Performance: detiene el timer para no consumir recursos de fondo.</summary>
    public void AlDesactivar()
    {
        _timer.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Carga las sesiones y los bloqueos activos.</summary>
    public async Task CargarAsync(bool esAutoRefresco = false)
    {
        if (_cargando || !PuedeActualizarUi || string.IsNullOrEmpty(Bd.Usuario)) return;

        var version = ++_version;
        _cargando = true;
        if (!esAutoRefresco)
        {
            UseWaitCursor = true;
            Tema.Cargando(_grdSesiones, true);
        }
        ActualizarBotones();

        try
        {
            // Consultamos sesiones y objetos bloqueados
            var tSesiones = await Bd.ConsultarAsync(SqlPerformance.Sesiones);
            var tBloqueos = await Bd.ConsultarAsync(SqlPerformance.ObjetosBloqueados);

            if (version != _version || !PuedeActualizarUi) return;

            _sidsBloqueadores = ServicioSesiones.ObtenerSidsBloqueadores(tSesiones);
            if (!_chkPausar.Checked)
            {
                _lblEstado.Text = $"Actualizado {DateTime.Now:HH:mm:ss} (5 s)";
                _lblEstado.ForeColor = Tema.TextoSecundario;
            }

            // Preservamos la selección actual
            var sidSeleccionado = FilaSeleccionada is { } f && f["SID"] is not DBNull
                ? Convert.ToInt32(f["SID"])
                : (int?)null;

            _grdSesiones.DataSource = tSesiones;

            // Configuración de encabezados y visibilidad
            var columnas = new Dictionary<string, (string Titulo, float Peso)>
            {
                ["SID"] = ("SID", 8),
                ["SERIAL#"] = ("Serial#", 9),
                ["USERNAME"] = ("Usuario", 14),
                ["STATUS"] = ("Estado", 10),
                ["BLOCKING_SESSION"] = ("Bloqueado por", 12),
                ["EVENT"] = ("Evento de espera", 22),
                ["SECONDS_IN_WAIT"] = ("Seg. espera", 10),
                ["MACHINE"] = ("Equipo", 14),
                ["PROGRAM"] = ("Programa", 15),
                ["SQL_ID"] = ("SQL_ID", 13),
                ["LOGON_TIME"] = ("Inicio sesión", 14),
                ["ES_LA_APP"] = ("Sesión App", 10)
            };

            foreach (DataGridViewColumn col in _grdSesiones.Columns)
            {
                if (columnas.TryGetValue(col.Name, out var c))
                {
                    col.HeaderText = c.Titulo;
                    col.FillWeight = c.Peso;
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }
                else
                {
                    col.Visible = false;
                }
            }

            // Restaurar selección si el SID sigue presente
            if (sidSeleccionado.HasValue)
            {
                foreach (DataGridViewRow r in _grdSesiones.Rows)
                {
                    if (r.DataBoundItem is DataRowView drv &&
                        drv.Row["SID"] is not DBNull &&
                        Convert.ToInt32(drv.Row["SID"]) == sidSeleccionado.Value)
                    {
                        r.Selected = true;
                        _grdSesiones.CurrentCell = r.Cells["SID"];
                        break;
                    }
                }
            }

            // Actualizar panel de bloqueos
            var listaBloqueos = ServicioSesiones.FormatearBloqueos(tSesiones, tBloqueos);
            if (listaBloqueos.Count == 0)
            {
                _txtBloqueos.Text = "✓ No hay bloqueos activos entre sesiones de usuario.";
                _txtBloqueos.ForeColor = Tema.Ok;
                _lblTituloBloqueos.ForeColor = Tema.TextoSecundario;
            }
            else
            {
                _txtBloqueos.Text = string.Join(Environment.NewLine, listaBloqueos);
                _txtBloqueos.ForeColor = Tema.Advertencia;
                _lblTituloBloqueos.ForeColor = Tema.Peligro;
            }
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi)
            {
                if (!esAutoRefresco)
                    Dialogos.Error(this, ex);
                else
                {
                    _lblEstado.Text = "Error en refresco: " + ex.Message;
                    _lblEstado.ForeColor = Tema.Peligro;
                }
            }
        }
        finally
        {
            _cargando = false;
            if (PuedeActualizarUi)
            {
                if (!esAutoRefresco)
                {
                    UseWaitCursor = false;
                    Tema.Cargando(_grdSesiones, false);
                }
                ActualizarBotones();
            }
        }
    }

    /// <summary>
    /// Pinta filas bloqueadas en amarillo y filas bloqueadoras en rojo.
    /// </summary>
    private void GrdSesiones_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null) return;
        if (_grdSesiones.Rows[e.RowIndex].DataBoundItem is not DataRowView drv) return;

        var fila = drv.Row;
        var sid = fila["SID"] is not DBNull ? Convert.ToInt32(fila["SID"]) : 0;
        var bloqueadoPor = fila["BLOCKING_SESSION"] is not DBNull ? Convert.ToInt32(fila["BLOCKING_SESSION"]) : 0;

        e.CellStyle.BackColor = _grdSesiones.DefaultCellStyle.BackColor;
        e.CellStyle.ForeColor = _grdSesiones.DefaultCellStyle.ForeColor;

        // Si la sesión está esperando un bloqueo (víctima): Amarillo
        if (bloqueadoPor > 0)
        {
            e.CellStyle.BackColor = Tema.AdvertenciaFondo;
            e.CellStyle.ForeColor = Tema.Advertencia;
        }
        // Si la sesión está bloqueando a otra (culpable): Rojo
        else if (_sidsBloqueadores.Contains(sid))
        {
            e.CellStyle.BackColor = Tema.PeligroFondo;
            e.CellStyle.ForeColor = Tema.Peligro;
        }
    }

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario) && !_cargando;
        _btnRefrescar.Enabled = conSesion;

        var fila = FilaSeleccionada;
        if (fila is null || !conSesion)
        {
            _btnMatar.Enabled = false;
            _btnVerSql.Enabled = false;
            return;
        }

        var esLaApp = fila["ES_LA_APP"]?.ToString() == "SI";
        _btnMatar.Enabled = !esLaApp;

        var sqlId = fila["SQL_ID"]?.ToString();
        _btnVerSql.Enabled = !string.IsNullOrWhiteSpace(sqlId);
    }

    private async Task MatarSesionSeleccionadaAsync()
    {
        var fila = FilaSeleccionada;
        if (fila is null) return;

        var esLaApp = fila["ES_LA_APP"]?.ToString() == "SI";
        if (esLaApp)
        {
            Dialogos.Error(this, new InvalidOperationException("No se puede matar la sesión de la propia aplicación."));
            return;
        }

        var sid = Convert.ToInt32(fila["SID"]);
        var serial = Convert.ToInt32(fila["SERIAL#"]);
        var usuario = fila["USERNAME"]?.ToString() ?? "sin usuario";

        var sql = ServicioSesiones.MatarSesion(sid, serial, esLaApp);

        if (!Dialogos.ConfirmarSql(this, $"Matar sesión {sid} ({usuario})", sql + ";", destructiva: true))
            return;

        try
        {
            UseWaitCursor = true;
            _btnMatar.Enabled = false;
            await Bd.EjecutarAsync(Modulo.Performance, "Matar sesión", sql);
            Dialogos.Exito(this, $"Sesión {sid},{serial} finalizada con éxito.");
            await CargarAsync(esAutoRefresco: false);
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi)
                Dialogos.Error(this, ex);
        }
        finally
        {
            if (PuedeActualizarUi)
            {
                UseWaitCursor = false;
                ActualizarBotones();
            }
        }
    }

    private async Task VerSqlActivoAsync()
    {
        var fila = FilaSeleccionada;
        if (fila is null) return;

        var sqlId = fila["SQL_ID"]?.ToString();
        if (string.IsNullOrWhiteSpace(sqlId))
        {
            Dialogos.Error(this, new InvalidOperationException("La sesión no tiene un SQL_ID registrado."));
            return;
        }

        var sid = fila["SID"];
        try
        {
            UseWaitCursor = true;
            var tabla = await Bd.ConsultarAsync(SqlPerformance.SqlDeSesion, Parametro.De("id", sqlId));
            if (!PuedeActualizarUi) return;

            if (tabla.Rows.Count == 0 || tabla.Rows[0]["SQL_FULLTEXT"] is DBNull)
            {
                Dialogos.Error(this, new InvalidOperationException($"La consulta {sqlId} ya no está en la memoria de Oracle (v$sql)."));
                return;
            }

            var sqlTexto = tabla.Rows[0]["SQL_FULLTEXT"]?.ToString() ?? "";
            Dialogos.VerSql(this, $"SQL activo (SID {sid}, SQL_ID {sqlId})", sqlTexto);
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi)
                Dialogos.Error(this, ex);
        }
        finally
        {
            if (PuedeActualizarUi)
                UseWaitCursor = false;
        }
    }
}

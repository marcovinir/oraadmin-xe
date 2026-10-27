using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Pestaña «Programados» de Respaldos (issue #30): respaldos automáticos con DBMS_SCHEDULER.
/// Crear, activar o desactivar, ejecutar ya y eliminar. Sus respaldos aparecen en el Historial como cualquier otro.
/// </summary>
public sealed class UcProgramados : UserControl
{
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly Label _lblPie = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnNueva = Tema.Boton("Programar respaldo", primario: true, icono: "");
    private readonly BotonPlano _btnEjecutar = Tema.Boton("Ejecutar ahora", icono: "");
    private readonly BotonPlano _btnActivar = Tema.Boton("Activar", icono: "");
    private readonly BotonPlano _btnDesactivar = Tema.Boton("Desactivar", icono: "");
    private readonly BotonPlano _btnEliminar = Tema.Boton("Eliminar", icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");

    private static IBaseDatos Bd => Sesion.Bd;

    /// <summary>Se dispara tras «Ejecutar ahora», para que el Historial se recargue.</summary>
    public event EventHandler? RespaldoLanzado;

    public UcProgramados()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnEliminar.Estilo = EstiloBoton.Peligro;

        var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        izquierda.Controls.AddRange([_btnNueva, _btnEjecutar, _btnActivar, _btnDesactivar, _btnEliminar]);
        var derecha = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        derecha.Controls.Add(_btnRefrescar);
        var barra = new Panel { Dock = DockStyle.Top, Height = 48 };
        barra.Controls.Add(izquierda);
        barra.Controls.Add(derecha);

        Tema.AplicarGrilla(_grilla);
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grilla.MultiSelect = false;
        _grilla.RowTemplate.Height = 36;
        _grilla.ShowCellToolTips = true;
        Tema.ColumnaEstado(_grilla, "TIPO", _ => TipoEstado.Acento);
        Tema.ColumnaEstado(_grilla, "STATE", t => t switch { "SCHEDULED" => TipoEstado.Ok, "RUNNING" => TipoEstado.Info, "DISABLED" => TipoEstado.Neutro, _ => TipoEstado.Peligro });
        Tema.MensajeVacio(_grilla, "No hay respaldos programados. Cree uno con «Programar respaldo».", "");
        _grilla.SelectionChanged += (_, _) => ActualizarBotones();
        _grilla.CellFormatting += Grilla_CellFormatting;

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 32 };
        pie.Controls.Add(_lblPie);

        Controls.Add(_grilla);
        Controls.Add(pie);
        Controls.Add(barra);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnNueva.Click += async (_, _) => await CrearAsync();
        _btnEjecutar.Click += async (_, _) => await AccionAsync("Ejecutar respaldo programado", ServicioProgramaciones.BloqueEjecutarAhora, false,
            j => $"{j} se está ejecutando en segundo plano. El respaldo aparecerá en «Historial» al terminar.", lanzaRespaldo: true);
        _btnActivar.Click += async (_, _) => await AccionAsync("Activar programación", j => ServicioProgramaciones.BloqueActivar(j, true), false, j => $"{j} activado.");
        _btnDesactivar.Click += async (_, _) => await AccionAsync("Desactivar programación", j => ServicioProgramaciones.BloqueActivar(j, false), false, j => $"{j} desactivado.");
        _btnEliminar.Click += async (_, _) => await AccionAsync("Eliminar programación",
            j => ServicioProgramaciones.BloqueEliminar(j, Seleccionado?["STATE"] is string), true, j => $"{j} eliminado. Sus respaldos siguen en el catálogo.");
        ActualizarBotones();
    }

    private DataRow? Seleccionado =>
        _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;

    public async Task CargarAsync(string? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        var anterior = seleccionar ?? Seleccionado?["NOMBRE_JOB"] as string;
        UseWaitCursor = true;
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlProgramaciones.Lista);
            _grilla.DataSource = tabla;
            Encabezados();
            foreach (DataGridViewRow fila in _grilla.Rows)
                if ((string)fila.Cells["NOMBRE_JOB"].Value == anterior)
                {
                    fila.Selected = true;
                    _grilla.CurrentCell = fila.Cells["NOMBRE_JOB"];
                }
            var activas = tabla.AsEnumerable().Count(r => r["STATE"] is "SCHEDULED" or "RUNNING");
            _lblPie.Text = $"{tabla.Rows.Count} programaciones · {activas} activas · Las corre el scheduler de Oracle aunque la app esté cerrada.";
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        finally
        {
            UseWaitCursor = false;
            ActualizarBotones();
        }
    }

    private void Encabezados()
    {
        var columnas = new Dictionary<string, (string Texto, float Peso)>
        {
            ["NOMBRE_JOB"] = ("Job", 18), ["TIPO"] = ("Tipo", 8), ["OBJETOS"] = ("Objetos", 15), ["FRECUENCIA"] = ("Frecuencia", 19),
            ["STATE"] = ("Estado", 10), ["PROXIMA"] = ("Próxima", 11), ["ULTIMA"] = ("Última", 11), ["RUN_COUNT"] = ("Veces", 7), ["FAILURE_COUNT"] = ("Fallas", 6)
        };
        foreach (DataGridViewColumn col in _grilla.Columns)
        {
            if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
            else col.Visible = false;
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd/MM HH:mm";
            if (col.Name is "RUN_COUNT" or "FAILURE_COUNT") col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        var orden = 0;
        foreach (var nombre in columnas.Keys) _grilla.Columns[nombre]!.DisplayIndex = orden++;
    }

    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView v) return;
        switch (_grilla.Columns[e.ColumnIndex].Name)
        {
            case "STATE" when v["STATE"] is DBNull:
                e.Value = "SIN JOB";   // la fila existe pero el job se borró por fuera de la app
                e.FormattingApplied = true;
                break;
            case "OBJETOS" when v["OBJETOS"] is DBNull:
                e.Value = "Toda la PDB";
                e.CellStyle.ForeColor = Tema.TextoSecundario;
                e.FormattingApplied = true;
                break;
            case "FAILURE_COUNT" when v["FAILURE_COUNT"] is not DBNull && Convert.ToInt32(v["FAILURE_COUNT"]) > 0:
                e.CellStyle.ForeColor = Tema.Peligro;
                break;
            case "FRECUENCIA" or "OBJETOS" when e.Value is string texto:
                _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
                break;
        }
    }

    private void ActualizarBotones()
    {
        var conSesion = !string.IsNullOrEmpty(Bd.Usuario);
        var estado = Seleccionado?["STATE"] as string;
        _btnNueva.Enabled = _btnRefrescar.Enabled = conSesion;
        _btnEjecutar.Enabled = estado == "SCHEDULED";
        _btnActivar.Enabled = estado == "DISABLED";
        _btnDesactivar.Enabled = estado == "SCHEDULED";
        _btnEliminar.Enabled = Seleccionado is not null;
    }

    private async Task CrearAsync()
    {
        try
        {
            var esquemas = Bd.ConsultarAsync(SqlRespaldos.Esquemas);
            var directorios = Bd.ConsultarAsync(SqlRespaldos.Directorios);
            await Task.WhenAll(esquemas, directorios);
            using var dlg = new DlgProgramacion(
                esquemas.Result.AsEnumerable().Select(r => (string)r[0]),
                directorios.Result.AsEnumerable().Select(r => new DirectorioRespaldo(Convert.ToInt32(r["ID_DIRECTORIO"]), (string)r["NOMBRE_ORACLE"], (string)r["RUTA_SO"])),
                async e => (await Bd.ConsultarAsync(SqlRespaldos.TablasDe, Parametro.De("e", e))).AsEnumerable().Select(r => (string)r[0]).ToArray());
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Solicitud is not { } s || dlg.Frecuencia is not { } f) return;

            if (Convert.ToInt32(await Bd.EscalarAsync(SqlProgramaciones.NombreOcupado, Parametro.De("n", s.Job))) > 0)
                throw new ArgumentException($"Ya existe una programación o un job llamado {s.Job}. Cambie el nombre.");
            var sql = ServicioProgramaciones.BloqueCrear(s, f);
            if (!Dialogos.ConfirmarSql(this, "Programar respaldo con DBMS_SCHEDULER", sql)) return;
            await Bd.EjecutarAsync(Modulo.Respaldos, $"Programar respaldo {s.Codigo}", sql);
            Dialogos.Exito(this, $"Programación {s.Job} creada ({f.RepeatInterval}).");
            await CargarAsync(s.Job);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task AccionAsync(string titulo, Func<string, string> bloque, bool destructiva, Func<string, string> exito, bool lanzaRespaldo = false)
    {
        if (Seleccionado?["NOMBRE_JOB"] is not string job) return;
        try
        {
            var sql = bloque(job);
            if (!Dialogos.ConfirmarSql(this, titulo, sql, destructiva)) return;
            await Bd.EjecutarAsync(Modulo.Respaldos, titulo, sql);
            Dialogos.Exito(this, exito(job));
            if (lanzaRespaldo) RespaldoLanzado?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        await CargarAsync(job);
    }
}

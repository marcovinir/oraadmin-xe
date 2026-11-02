using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Pestaña «Historial» de Respaldos (issue #12): catálogo de V_RESPALDOS con sus objetos, duración, tamaño
/// y estado con color; Ver log y limpieza de jobs de Data Pump que quedaron colgados.
/// </summary>
public sealed class UcCatalogoRespaldos : UserControl
{
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly Label _lblPie = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnLog = Tema.Boton("Ver log", primario: true, icono: "");
    private readonly BotonPlano _btnLimpiar = Tema.Boton("Limpiar jobs colgados", icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");

    private static IBaseDatos Bd => Sesion.Bd;

    public UcCatalogoRespaldos()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnLimpiar.Estilo = EstiloBoton.Peligro;

        var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        izquierda.Controls.AddRange([_btnLog, _btnLimpiar]);
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
        Tema.ColumnaEstado(_grilla, "ESTADO");
        Tema.ColumnaEstado(_grilla, "TIPO", _ => TipoEstado.Acento);
        Tema.MensajeVacio(_grilla, "Todavía no hay respaldos. Haga el primero en «Nuevo respaldo».", "");
        _grilla.SelectionChanged += (_, _) => ActualizarBotones();
        _grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) VerLog(); };
        _grilla.CellFormatting += Grilla_CellFormatting;

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 32 };
        pie.Controls.Add(_lblPie);

        Controls.Add(_grilla);
        Controls.Add(pie);
        Controls.Add(barra);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnLog.Click += (_, _) => VerLog();
        _btnLimpiar.Click += async (_, _) => await LimpiarAsync();
        ActualizarBotones();
    }

    /// <summary>Mientras corre un respaldo no se limpian jobs (el que corre todavía no tiene su job creado).</summary>
    public bool RespaldoEnCurso { get; set; }

    private DataRow? Seleccionado =>
        _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;

    public async Task CargarAsync(int? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        UseWaitCursor = true;
        Tema.Cargando(_grilla, true);
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlRespaldos.Catalogo);
            _grilla.DataSource = tabla;
            Encabezados();
            if (seleccionar is not null)
                foreach (DataGridViewRow fila in _grilla.Rows)
                    if (Convert.ToInt32(fila.Cells["ID_RESPALDO"].Value) == seleccionar)
                    {
                        fila.Selected = true;
                        _grilla.CurrentCell = fila.Cells["TIPO"];
                    }
            var completados = tabla.AsEnumerable().Where(r => (string)r["ESTADO"] == "COMPLETADO").ToList();
            var mb = completados.Sum(r => r["TAMANO_MB"] is DBNull ? 0 : Convert.ToDouble(r["TAMANO_MB"]));
            var errores = tabla.AsEnumerable().Count(r => (string)r["ESTADO"] == "ERROR");
            _lblPie.Text = $"{tabla.Rows.Count} respaldos · {completados.Count} completados ({mb:0.##} MB)" +
                           (errores > 0 ? $" · {errores} con error (pase el mouse sobre la observación para ver el ORA)" : "") +
                           " · Doble clic en una fila para ver su log.";
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            Tema.Cargando(_grilla, false);
            UseWaitCursor = false;
            ActualizarBotones();
        }
    }

    private void Encabezados()
    {
        var columnas = new Dictionary<string, (string Texto, float Peso)>
        {
            ["TIPO"] = ("Tipo", 8), ["OBJETOS"] = ("Objetos", 17), ["ARCHIVO_DUMP"] = ("Archivo", 19), ["DIRECTORIO"] = ("Directorio", 13),
            ["FECHA_INICIO"] = ("Inicio", 14), ["DURACION_SEG"] = ("Duración", 9), ["TAMANO_MB"] = ("MB", 7),
            ["ESTADO"] = ("Estado", 12), ["OBSERVACION"] = ("Observación", 14)
        };
        foreach (DataGridViewColumn col in _grilla.Columns)
        {
            if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
            else col.Visible = false;
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd/MM/yy HH:mm";
            if (col.Name is "DURACION_SEG" or "TAMANO_MB") col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        _grilla.Columns["TAMANO_MB"]!.DefaultCellStyle.Format = "0.00";
        // Mismo orden en pantalla que en el diccionario
        var orden = 0;
        foreach (var nombre in columnas.Keys) _grilla.Columns[nombre]!.DisplayIndex = orden++;
    }

    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView v) return;
        var columna = _grilla.Columns[e.ColumnIndex].Name;
        if (columna == "OBJETOS" && v["OBJETOS"] is DBNull && (string)v["TIPO"] == "FULL")
        {
            e.Value = "Toda la PDB";
            e.CellStyle.ForeColor = Tema.TextoSecundario;
            e.FormattingApplied = true;
        }
        if (columna == "DURACION_SEG" && v["DURACION_SEG"] is not DBNull)
        {
            e.Value = Duracion(Convert.ToInt32(v["DURACION_SEG"]));
            e.FormattingApplied = true;
        }
        if (columna is "OBSERVACION" or "OBJETOS" or "DIRECTORIO" && v[columna] is string texto)
            _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
        if (columna == "OBSERVACION" && (string)v["ESTADO"] == "ERROR")
            e.CellStyle.ForeColor = Tema.Peligro;
    }

    /// <summary>140 → «2 min 20 s».</summary>
    public static string Duracion(int segundos) =>
        segundos < 60 ? $"{segundos} s" : $"{segundos / 60} min {segundos % 60:00} s";

    private void ActualizarBotones()
    {
        _btnLog.Enabled = Seleccionado is not null;
        _btnLimpiar.Enabled = !RespaldoEnCurso && !string.IsNullOrEmpty(Bd.Usuario);
        _btnRefrescar.Enabled = !string.IsNullOrEmpty(Bd.Usuario);
    }

    private void VerLog()
    {
        if (Seleccionado is not { } fila) return;
        DlgLog.Mostrar(this, Path.Combine((string)fila["RUTA_SO"], (string)fila["ARCHIVO_LOG"]));
    }

    /// <summary>
    /// Borra la tabla maestra de los jobs que quedaron a medias (destructivo) y marca en ERROR
    /// los respaldos que quedaron EN_PROCESO sin job.
    /// </summary>
    private async Task LimpiarAsync()
    {
        try
        {
            var jobs = (await Bd.ConsultarAsync(SqlRespaldos.JobsColgados)).AsEnumerable()
                .Select(r => Identificador.Validar((string)r["JOB_NAME"], "job")).ToList();
            var sentencias = jobs.Select(j => $"DROP TABLE {Bd.Usuario}.\"{j}\" PURGE").ToList();
            sentencias.Add(SqlRespaldos.MarcarInterrumpidos.Trim());
            var aviso = jobs.Count == 0
                ? "-- No hay jobs de Data Pump colgados.\r\n-- Solo se marcarán en ERROR los respaldos que quedaron EN_PROCESO sin job.\r\n"
                : $"-- {jobs.Count} job(s) colgado(s): se borra su tabla maestra.\r\n";
            if (!Dialogos.ConfirmarSql(this, "Limpiar jobs colgados", aviso + string.Join(";\r\n", sentencias) + ";", destructiva: jobs.Count > 0))
                return;

            foreach (var sql in sentencias)
                await Bd.EjecutarAsync(Modulo.Respaldos, sql.StartsWith("DROP") ? "Limpiar job colgado" : "Marcar respaldos interrumpidos", sql);
            Dialogos.Exito(this, jobs.Count == 0 ? "No había jobs colgados." : $"Se limpiaron {jobs.Count} job(s) colgado(s).");
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        await CargarAsync();
    }
}

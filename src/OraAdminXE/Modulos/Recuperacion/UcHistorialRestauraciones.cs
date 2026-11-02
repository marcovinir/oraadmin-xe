using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Respaldos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>
/// Pestaña «Historial» de Recuperación (issue #14): cada restauración con sus objetos, el esquema destino,
/// la acción si la tabla existía, la duración y el estado con color. Ver log sobre la fila elegida.
/// </summary>
public sealed class UcHistorialRestauraciones : UserControl
{
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly Label _lblPie = new() { Dock = DockStyle.Fill, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly BotonPlano _btnLog = Tema.Boton("Ver log", primario: true, icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");

    private static IBaseDatos Bd => Sesion.Bd;

    public UcHistorialRestauraciones()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;

        var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        izquierda.Controls.Add(_btnLog);
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
        Tema.ColumnaEstado(_grilla, "ACCION_EXISTENTE", t => t is "REPLACE" or "TRUNCATE" ? TipoEstado.Peligro : TipoEstado.Info);
        Tema.MensajeVacio(_grilla, "Todavía no hay restauraciones. Haga la primera en «Restaurar».", "");
        _grilla.SelectionChanged += (_, _) => _btnLog.Enabled = Seleccionado is not null;
        _grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) VerLog(); };
        _grilla.CellFormatting += Grilla_CellFormatting;

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 32 };
        pie.Controls.Add(_lblPie);

        Controls.Add(_grilla);
        Controls.Add(pie);
        Controls.Add(barra);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnLog.Click += (_, _) => VerLog();
        _btnLog.Enabled = false;
    }

    private DataRow? Seleccionado =>
        _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView v ? v.Row : null;

    public async Task CargarAsync(int? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        UseWaitCursor = true;
        Tema.Cargando(_grilla, true);
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlRecuperacion.Historial);
            _grilla.DataSource = tabla;
            Encabezados();
            if (seleccionar is not null)
                foreach (DataGridViewRow fila in _grilla.Rows)
                    if (Convert.ToInt32(fila.Cells["ID_RESTAURACION"].Value) == seleccionar)
                    {
                        fila.Selected = true;
                        _grilla.CurrentCell = fila.Cells["FECHA_INICIO"];
                    }
            var errores = tabla.AsEnumerable().Count(r => (string)r["ESTADO"] == "ERROR");
            var avisos = tabla.AsEnumerable().Count(r => (string)r["ESTADO"] == "COMPLETADO" && r["OBSERVACION"] is string);
            _lblPie.Text = $"{tabla.Rows.Count} restauraciones" +
                           (errores > 0 ? $" · {errores} con error" : "") +
                           (avisos > 0 ? $" · {avisos} completadas con avisos (ver observación)" : "") +
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
        }
    }

    private void Encabezados()
    {
        var columnas = new Dictionary<string, (string Texto, float Peso)>
        {
            ["FECHA_INICIO"] = ("Fecha", 12), ["ARCHIVO_DUMP"] = ("Desde el respaldo", 20), ["OBJETOS"] = ("Objetos", 18),
            ["ESQUEMA_DESTINO"] = ("Destino", 11), ["ACCION_EXISTENTE"] = ("Si existe", 10), ["DURACION_SEG"] = ("Duración", 9),
            ["ESTADO"] = ("Estado", 12), ["OBSERVACION"] = ("Observación", 16)
        };
        foreach (DataGridViewColumn col in _grilla.Columns)
        {
            if (columnas.TryGetValue(col.Name, out var c)) { col.HeaderText = c.Texto; col.FillWeight = c.Peso; }
            else col.Visible = false;
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd/MM/yy HH:mm";
        }
        _grilla.Columns["DURACION_SEG"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        var orden = 0;
        foreach (var nombre in columnas.Keys) _grilla.Columns[nombre]!.DisplayIndex = orden++;
    }

    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView v) return;
        switch (_grilla.Columns[e.ColumnIndex].Name)
        {
            case "ESQUEMA_DESTINO" when v["ESQUEMA_DESTINO"] is DBNull:
                e.Value = "El mismo";
                e.CellStyle.ForeColor = Tema.TextoSecundario;
                e.FormattingApplied = true;
                break;
            case "OBJETOS" when v["OBJETOS"] is DBNull:
                e.Value = "Todo el respaldo";
                e.CellStyle.ForeColor = Tema.TextoSecundario;
                e.FormattingApplied = true;
                break;
            case "DURACION_SEG" when v["DURACION_SEG"] is not DBNull:
                e.Value = UcCatalogoRespaldos.Duracion(Convert.ToInt32(v["DURACION_SEG"]));
                e.FormattingApplied = true;
                break;
            case "OBSERVACION" when v["OBSERVACION"] is string texto:
                _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
                e.CellStyle.ForeColor = (string)v["ESTADO"] == "ERROR" ? Tema.Peligro : Tema.Advertencia;
                break;
            case "OBJETOS" or "ARCHIVO_DUMP" when e.Value is string texto:
                _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
                break;
        }
    }

    private void VerLog()
    {
        if (Seleccionado is not { } fila) return;
        DlgLog.Mostrar(this, Path.Combine((string)fila["RUTA_SO"], (string)fila["ARCHIVO_LOG"]));
    }
}

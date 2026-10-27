using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>
/// Tablespaces: consulta de uso y archivos físicos en modo solo lectura (issue #15).
/// Las acciones CREATE, ALTER y DROP corresponden a la issue #16.
/// </summary>
public partial class UcTablespaces : ModuloBase
{
    private readonly SemaphoreSlim _cargaArchivos = new(1, 1);
    private bool _cargando;
    private int _solicitudArchivos;
    private string? _tablespaceSeleccionado;

    public UcTablespaces()
    {
        InitializeComponent();
        ConfigurarGrilla(grdTablespaces, "Sin datos de tablespaces", "\uEDA2");
        ConfigurarGrilla(grdArchivos, "Seleccione un tablespace para ver sus archivos", "\uE8B7");

        Tema.ColumnaEstado(grdTablespaces, "STATUS");
        Tema.ColumnaEstado(grdTablespaces, "CONTENTS", _ => TipoEstado.Info);
        Tema.ColumnaEstado(grdTablespaces, "AUTOEXTEND", texto => texto == "MIXED" ? TipoEstado.Advertencia : Estados.Clasificar(texto));
        Tema.ColumnaEstado(grdArchivos, "TIPO_ARCHIVO", _ => TipoEstado.Info);
        Tema.ColumnaEstado(grdArchivos, "AUTOEXTENSIBLE");
        Tema.ColumnaEstado(grdArchivos, "ESTADO");
        grdTablespaces.CellPainting += GrdUso_CellPainting;
        grdArchivos.CellPainting += GrdUso_CellPainting;
        grdArchivos.CellToolTipTextNeeded += GrdArchivos_CellToolTipTextNeeded;
    }

    public override Modulo Modulo => Modulo.Tablespaces;

    public override Task AlActivarAsync() => CargarAsync();

    public override void AlDesactivar()
    {
        // Invalida una carga de detalle en espera antes de cambiar de módulo.
        _solicitudArchivos++;
    }

    private async void BtnRefrescar_Click(object? sender, EventArgs e) => await CargarAsync();

    private async Task CargarAsync()
    {
        if (_cargando || !PuedeActualizarUi || string.IsNullOrEmpty(Bd.Usuario)) return;

        var seleccionAnterior = _tablespaceSeleccionado;
        _cargando = true;
        btnRefrescar.Enabled = false;
        UseWaitCursor = true;
        Tema.Cargando(grdTablespaces, true);
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlTablespaces.Uso);
            if (!PuedeActualizarUi) return;
            MostrarTablespaces(tabla);

            var seleccionado = RestaurarSeleccion(tabla, seleccionAnterior);
            _tablespaceSeleccionado = seleccionado;
            if (seleccionado is null)
            {
                MostrarArchivos(new DataTable());
                return;
            }

            await CargarArchivosAsync(seleccionado, ++_solicitudArchivos);
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
                Tema.Cargando(grdTablespaces, false);
                btnRefrescar.Enabled = true;
                UseWaitCursor = false;
            }
            _cargando = false;
        }
    }

    private void GrdTablespaces_SelectionChanged(object? sender, EventArgs e)
    {
        if (_cargando || !PuedeActualizarUi) return;

        var tablespace = TablespaceSeleccionado();
        if (tablespace is null || tablespace == _tablespaceSeleccionado) return;

        _tablespaceSeleccionado = tablespace;
        var solicitud = ++_solicitudArchivos;
        try
        {
            BeginInvoke(new Action(() => _ = CargarArchivosAsync(tablespace, solicitud)));
        }
        catch (InvalidOperationException) when (!PuedeActualizarUi)
        {
            // El control se estaba cerrando entre la comprobación anterior y BeginInvoke.
        }
    }

    private async Task CargarArchivosAsync(string tablespace, int solicitud)
    {
        await _cargaArchivos.WaitAsync();
        try
        {
            // Si hubo selecciones rápidas, solo la respuesta más reciente actualiza la UI.
            if (!PuedeActualizarUi || solicitud != _solicitudArchivos) return;

            Tema.Cargando(grdArchivos, true);
            var tabla = await Bd.ConsultarAsync(SqlTablespaces.Archivos, Parametro.De("tablespace", tablespace));
            if (!PuedeActualizarUi || solicitud != _solicitudArchivos) return;
            MostrarArchivos(tabla);
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi && solicitud == _solicitudArchivos)
                Dialogos.Error(this, ex);
        }
        finally
        {
            if (PuedeActualizarUi && solicitud == _solicitudArchivos)
                Tema.Cargando(grdArchivos, false);
            _cargaArchivos.Release();
        }
    }

    private void ConfigurarGrilla(DataGridView grilla, string mensajeVacio, string icono)
    {
        Tema.AplicarGrilla(grilla);
        grilla.AutoSizeColumnsMode = ReferenceEquals(grilla, grdArchivos)
            ? DataGridViewAutoSizeColumnsMode.None
            : DataGridViewAutoSizeColumnsMode.Fill;
        grilla.ScrollBars = ReferenceEquals(grilla, grdArchivos) ? ScrollBars.Both : ScrollBars.Vertical;
        grilla.RowTemplate.Height = 36;
        Tema.MensajeVacio(grilla, mensajeVacio, icono);
    }

    private void MostrarTablespaces(DataTable tabla)
    {
        grdTablespaces.DataSource = tabla;
        ConfigurarColumnas(grdTablespaces,
            ("TABLESPACE_NAME", "Tablespace", 16), ("CONTENTS", "Tipo", 13), ("STATUS", "Estado", 11),
            ("TOTAL_MB", "Total (MB)", 10), ("USADO_MB", "Usado (MB)", 10), ("LIBRE_MB", "Libre (MB)", 10),
            ("PCT_USADO", "% usado", 24), ("AUTOEXTEND", "Autoextend", 10));
    }

    private void MostrarArchivos(DataTable tabla)
    {
        grdArchivos.DataSource = tabla;
        ConfigurarColumnas(grdArchivos,
            ("TIPO_ARCHIVO", "Archivo", 10), ("FILE_ID", "ID", 6), ("FILE_NAME", "Ruta", 31),
            ("TAMANO_MB", "Tamaño (MB)", 10), ("USADO_MB", "Usado (MB)", 10), ("LIBRE_MB", "Libre (MB)", 10),
            ("PCT_USADO", "% usado", 20), ("AUTOEXTENSIBLE", "Autoextend", 10), ("MAX_MB", "Máximo (MB)", 11),
            ("ESTADO", "Estado", 12));
        ConfigurarAnchosArchivos();
    }

    private static void ConfigurarColumnas(DataGridView grilla, params (string Nombre, string Encabezado, float Peso)[] columnas)
    {
        foreach (var (nombre, encabezado, peso) in columnas)
        {
            var columna = grilla.Columns[nombre];
            if (columna is null) continue;
            columna.HeaderText = encabezado;
            columna.FillWeight = peso;
            if (nombre is "FILE_ID" or "TOTAL_MB" or "USADO_MB" or "LIBRE_MB" or "TAMANO_MB" or "MAX_MB")
                columna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void ConfigurarAnchosArchivos()
    {
        var anchos = new (string Nombre, int Ancho, int Minimo)[]
        {
            ("TIPO_ARCHIVO", 95, 85), ("FILE_ID", 65, 60), ("FILE_NAME", 520, 360),
            ("TAMANO_MB", 115, 105), ("USADO_MB", 110, 100), ("LIBRE_MB", 110, 100),
            ("PCT_USADO", 150, 130), ("AUTOEXTENSIBLE", 120, 110), ("MAX_MB", 125, 110), ("ESTADO", 120, 100)
        };
        foreach (var (nombre, ancho, minimo) in anchos)
        {
            var columna = grdArchivos.Columns[nombre];
            if (columna is null) continue;
            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columna.MinimumWidth = minimo;
            columna.Width = ancho;
        }
    }

    private void GrdArchivos_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || grdArchivos.Columns[e.ColumnIndex].Name != "FILE_NAME") return;
        if (grdArchivos.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string ruta && ruta.Length > 0)
            e.ToolTipText = ruta;
    }

    private string? RestaurarSeleccion(DataTable tabla, string? preferido)
    {
        if (tabla.Rows.Count == 0) return null;
        var nombre = tabla.AsEnumerable().Select(f => f.Field<string>("TABLESPACE_NAME"))
            .FirstOrDefault(n => n == preferido) ?? tabla.Rows[0].Field<string>("TABLESPACE_NAME");

        foreach (DataGridViewRow fila in grdTablespaces.Rows)
        {
            if (!string.Equals(fila.Cells["TABLESPACE_NAME"].Value as string, nombre, StringComparison.Ordinal)) continue;
            fila.Selected = true;
            grdTablespaces.CurrentCell = fila.Cells["TABLESPACE_NAME"];
            break;
        }
        return nombre;
    }

    private string? TablespaceSeleccionado() => grdTablespaces.CurrentRow?.DataBoundItem is DataRowView fila
        ? fila.Row.Field<string>("TABLESPACE_NAME")
        : null;

    private bool PuedeActualizarUi => !IsDisposed && !Disposing;

    /// <summary>Dibuja porcentaje real con una barra verde, amarilla o roja.</summary>
    private static void GrdUso_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grilla || e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null ||
            grilla.Columns[e.ColumnIndex].Name != "PCT_USADO") return;

        e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
        var porcentaje = ConvertirPorcentaje(e.Value);
        if (porcentaje is null)
        {
            TextRenderer.DrawText(e.Graphics, "N/D", Tema.Fuente, e.CellBounds, Tema.TextoSecundario,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            e.Handled = true;
            return;
        }

        var normalizado = IndicadorUsoTablespace.Normalizar(porcentaje)!.Value;
        var ancho = Math.Max(10, e.CellBounds.Width - 70);
        var barra = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + e.CellBounds.Height / 2 - 4, ancho, 8);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var fondo = Tema.Redondeado(barra, 4))
        using (var pincel = new SolidBrush(Tema.SuperficieAlta))
            e.Graphics.FillPath(pincel, fondo);
        var lleno = (int)(barra.Width * normalizado / 100);
        if (lleno > 0)
        {
            using var relleno = Tema.Redondeado(new Rectangle(barra.X, barra.Y, Math.Max(2, lleno), barra.Height), 4);
            using var color = new SolidBrush(Tema.ColoresDe(IndicadorUsoTablespace.Clasificar(porcentaje)).Texto);
            e.Graphics.FillPath(color, relleno);
        }

        var texto = new Rectangle(barra.Right + 6, e.CellBounds.Y, 56, e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics, $"{normalizado:0.0} %", Tema.Fuente, texto, Tema.Texto,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        e.Handled = true;
    }

    private static double? ConvertirPorcentaje(object? valor)
    {
        if (valor is null or DBNull) return null;
        try { return Convert.ToDouble(valor); }
        catch (Exception) when (valor is IConvertible) { return null; }
    }
}

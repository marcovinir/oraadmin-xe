using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Dashboard;

/// <summary>
/// Inicio: estado de la instancia, tiempo arriba, sesiones, último respaldo y uso de tablespaces con semáforo.
/// </summary>
public partial class UcDashboard : ModuloBase
{
    private const double UmbralAdvertencia = 70;
    private const double UmbralPeligro = 85;

    public UcDashboard()
    {
        InitializeComponent();
        Tema.AplicarGrilla(grdTablespaces);
        grdTablespaces.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grdTablespaces.RowTemplate.Height = 36;
        Tema.ColumnaEstado(grdTablespaces, "STATUS");
        Tema.ColumnaEstado(grdTablespaces, "AUTOEXTEND");
        Tema.ColumnaEstado(grdTablespaces, "CONTENTS", _ => TipoEstado.Info);
        Tema.MensajeVacio(grdTablespaces, "Sin datos de tablespaces", "");
    }

    private double _pctLimite;

    public override Modulo Modulo => Modulo.General;

    public override Task AlActivarAsync() => CargarAsync();

    private async void BtnRefrescar_Click(object? sender, EventArgs e) => await CargarAsync();

    private async Task CargarAsync()
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;   // sin sesión (diseñador o pruebas)

        btnRefrescar.Enabled = false;
        UseWaitCursor = true;
        try
        {
            var instancia = Bd.ConsultarAsync(SqlDashboard.Instancia);
            var sesiones = Bd.ConsultarAsync(SqlDashboard.Sesiones);
            var respaldo = Bd.ConsultarAsync(SqlDashboard.UltimoRespaldo);
            var tablespaces = Bd.ConsultarAsync(SqlDashboard.Tablespaces);
            var datosGb = Bd.EscalarAsync(SqlDashboard.DatosUsuario);
            await Task.WhenAll(instancia, sesiones, respaldo, tablespaces, datosGb);

            MostrarInstancia(instancia.Result.Rows[0]);
            MostrarSesiones(sesiones.Result.Rows[0]);
            MostrarRespaldo(respaldo.Result.Rows.Count > 0 ? respaldo.Result.Rows[0] : null);
            MostrarTablespaces(tablespaces.Result);
            MostrarLimite(Convert.ToDouble(datosGb.Result ?? 0));
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            btnRefrescar.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void MostrarInstancia(DataRow fila)
    {
        var estado = (string)fila["STATUS"];
        tarInstancia.Valor = estado;
        tarInstancia.Detalle = $"{fila["INSTANCE_NAME"]} {fila["VERSION_FULL"]} · PDB {fila["CONTENEDOR"]}";
        tarInstancia.Acento = estado == "OPEN" ? Tema.Ok : Tema.Peligro;

        tarActividad.Valor = Duracion(Convert.ToDouble(fila["DIAS_ARRIBA"]));
        tarActividad.Detalle = $"Desde {(DateTime)fila["STARTUP_TIME"]:dd/MM/yyyy HH:mm}";
        tarActividad.Acento = Tema.Acento;
    }

    private void MostrarSesiones(DataRow fila)
    {
        tarSesiones.Valor = $"{fila["ACTIVAS"]} / {fila["TOTAL"]}";
        tarSesiones.Detalle = "Activas / total";
        tarSesiones.Acento = Tema.Acento;
    }

    private void MostrarRespaldo(DataRow? fila)
    {
        if (fila is null)
        {
            tarRespaldo.Valor = "Sin respaldos";
            tarRespaldo.Detalle = "Haga el primero desde Respaldos";
            tarRespaldo.Acento = Tema.TextoSecundario;
            return;
        }

        var estado = (string)fila["ESTADO"];
        tarRespaldo.Valor = estado switch
        {
            "COMPLETADO" => "Completado",
            "EN_PROCESO" => "En proceso",
            "ERROR" => "Con error",
            _ => "Eliminado"
        };
        var tamano = fila["TAMANO_MB"] is DBNull ? "" : $" · {Convert.ToDouble(fila["TAMANO_MB"]):0.##} MB";
        tarRespaldo.Detalle = $"{fila["TIPO"]} · {(DateTime)fila["FECHA_INICIO"]:dd/MM HH:mm}{tamano}";
        tarRespaldo.Acento = estado switch
        {
            "COMPLETADO" => Tema.Ok,
            "EN_PROCESO" => Tema.Advertencia,
            "ERROR" => Tema.Peligro,
            _ => Tema.TextoSecundario
        };
    }

    private void MostrarTablespaces(DataTable tabla)
    {
        grdTablespaces.DataSource = tabla;
        var columnas = new (string Nombre, string Encabezado, float Peso)[]
        {
            ("TABLESPACE_NAME", "Tablespace", 18), ("CONTENTS", "Tipo", 12), ("STATUS", "Estado", 10),
            ("TOTAL_MB", "Total (MB)", 10), ("USADO_MB", "Usado (MB)", 10), ("PCT_USADO", "% usado", 30),
            ("AUTOEXTEND", "Autoextend", 10)
        };
        foreach (var (nombre, encabezado, peso) in columnas)
        {
            var col = grdTablespaces.Columns[nombre]!;
            col.HeaderText = encabezado;
            col.FillWeight = peso;
            if (nombre is "TOTAL_MB" or "USADO_MB")
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void MostrarLimite(double gb)
    {
        var pct = gb / SqlDashboard.LimiteXeGb * 100;
        lblLimite.Text = $"Datos de usuario: {gb:0.##} GB de {SqlDashboard.LimiteXeGb} GB permitidos en XE ({pct:0.#} %)";
        _pctLimite = Math.Clamp(pct, 0, 100);
        barLimite.Invalidate();
    }

    private void BarLimite_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var area = new Rectangle(0, 0, barLimite.Width - 1, barLimite.Height - 1);
        using (var fondo = Tema.Redondeado(area, 3))
        using (var pincel = new SolidBrush(Tema.SuperficieAlta))
            e.Graphics.FillPath(pincel, fondo);
        var lleno = (int)(area.Width * _pctLimite / 100);
        if (lleno < 2) lleno = 2;
        using var relleno = Tema.Redondeado(new Rectangle(0, 0, lleno, area.Height), 3);
        using var color = new SolidBrush(ColorSemaforo(_pctLimite));
        e.Graphics.FillPath(color, relleno);
    }

    /// <summary>Dibuja el % usado como una barra de color (verde, amarillo o rojo) con el número encima.</summary>
    private void GrdTablespaces_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics is null || grdTablespaces.Columns[e.ColumnIndex].Name != "PCT_USADO" || e.Value is DBNull or null)
            return;

        var pct = Convert.ToDouble(e.Value);
        e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);

        var area = Rectangle.Inflate(e.CellBounds, -8, -9);
        area.Width -= 56;
        area = new Rectangle(area.X, area.Y + area.Height / 2 - 4, area.Width, 8);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var fondo = Tema.Redondeado(area, 4))
        using (var pincel = new SolidBrush(Tema.SuperficieAlta))
            e.Graphics.FillPath(pincel, fondo);
        var ancho = Math.Max(8, (int)(area.Width * Math.Clamp(pct, 0, 100) / 100));
        using (var relleno = Tema.Redondeado(new Rectangle(area.X, area.Y, ancho, area.Height), 4))
        using (var color = new SolidBrush(ColorSemaforo(pct)))
            e.Graphics.FillPath(color, relleno);

        var texto = new Rectangle(area.Right + 6, e.CellBounds.Y, 50, e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics, $"{pct:0.0} %", Tema.Fuente, texto, Tema.Texto,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        e.Handled = true;
    }

    private static Color ColorSemaforo(double pct) =>
        pct >= UmbralPeligro ? Tema.Peligro : pct >= UmbralAdvertencia ? Tema.Advertencia : Tema.Ok;

    private static string Duracion(double dias)
    {
        var t = TimeSpan.FromDays(dias);
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays} d {t.Hours} h"
             : t.TotalHours >= 1 ? $"{t.Hours} h {t.Minutes} min"
             : $"{t.Minutes} min";
    }
}

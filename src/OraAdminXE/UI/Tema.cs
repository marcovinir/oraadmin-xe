using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OraAdminXE.UI;

/// <summary>
/// Sistema visual de la app: tema oscuro «Medianoche violeta» (issue #38).
/// Usar SIEMPRE estos colores y helpers. <see cref="Aplicar"/> recorre los controles y les pone el tema,
/// así que lo que se diseñe en Visual Studio con colores por defecto se ve oscuro al correr la app.
/// </summary>
public static class Tema
{
    // Fondos, de más oscuro a más claro
    public static readonly Color MenuFondo       = Color.FromArgb(10, 11, 16);    // menú lateral
    public static readonly Color Fondo           = Color.FromArgb(14, 16, 22);    // fondo de pantallas
    public static readonly Color Superficie      = Color.FromArgb(20, 22, 31);    // tarjetas, grillas, diálogos
    public static readonly Color SuperficieAlta  = Color.FromArgb(25, 28, 39);    // campos, botones secundarios, encabezados
    public static readonly Color Borde           = Color.FromArgb(35, 38, 56);
    public static readonly Color BordeFuerte     = Color.FromArgb(52, 56, 78);

    // Texto
    public static readonly Color Texto           = Color.FromArgb(231, 232, 238);
    public static readonly Color TextoSecundario = Color.FromArgb(139, 144, 163);

    // Acento violeta
    public static readonly Color Acento          = Color.FromArgb(124, 108, 242);
    public static readonly Color AcentoHover     = Color.FromArgb(142, 128, 248);
    public static readonly Color AcentoSuave     = Color.FromArgb(30, 27, 58);    // fondo de lo seleccionado
    public static readonly Color AcentoTexto     = Color.FromArgb(180, 169, 255);

    // Menú lateral (alias)
    public static readonly Color MenuTexto       = TextoSecundario;
    public static readonly Color MenuHover       = Color.FromArgb(21, 23, 33);
    public static readonly Color MenuActivo      = AcentoSuave;

    // Semáforo: color de texto y fondo de pastilla
    public static readonly Color Ok              = Color.FromArgb(74, 222, 128);
    public static readonly Color OkFondo         = Color.FromArgb(14, 42, 30);
    public static readonly Color Advertencia     = Color.FromArgb(251, 191, 36);
    public static readonly Color AdvertenciaFondo = Color.FromArgb(43, 34, 16);
    public static readonly Color Peligro         = Color.FromArgb(248, 113, 113);
    public static readonly Color PeligroFondo    = Color.FromArgb(46, 20, 24);
    public static readonly Color Info            = Color.FromArgb(96, 165, 250);
    public static readonly Color InfoFondo       = Color.FromArgb(17, 33, 58);

    public static readonly Font Fuente           = new("Segoe UI", 10f);
    public static readonly Font FuenteNegrita    = new("Segoe UI Semibold", 10f);
    public static readonly Font FuenteTitulo     = new("Segoe UI Semibold", 16f);
    public static readonly Font FuenteSubtitulo  = new("Segoe UI", 10f, FontStyle.Italic);
    public static readonly Font FuenteMenu       = new("Segoe UI", 10.5f);
    public static readonly Font FuenteMenuActiva = new("Segoe UI Semibold", 10.5f);
    public static readonly Font FuenteLogo       = new("Segoe UI Semibold", 13f);
    public static readonly Font FuentePequena    = new("Segoe UI Semibold", 8.5f);
    public static readonly Font FuenteCodigo     = new("Cascadia Mono", 10f);
    public static readonly Font FuenteIconos     = new("Segoe MDL2 Assets", 12f);

    /// <summary>Botón estándar de barra de acciones (atajo de <see cref="BotonPlano"/>).</summary>
    public static BotonPlano Boton(string texto, bool primario = false, string? icono = null) =>
        new() { Text = texto, Estilo = primario ? EstiloBoton.Primario : EstiloBoton.Secundario, Icono = icono };

    // ---------------------------------------------------------------- aplicar el tema

    // Colores que ponía el tema claro anterior o el diseñador de Visual Studio → su equivalente oscuro.
    private static readonly Dictionary<int, Color> Equivalentes = new()
    {
        [Color.White.ToArgb()] = Superficie,
        [Color.FromArgb(248, 250, 252).ToArgb()] = Fondo,
        [Color.FromArgb(30, 41, 59).ToArgb()] = MenuFondo,
        [Color.FromArgb(15, 23, 42).ToArgb()] = Texto,
        [Color.FromArgb(100, 116, 139).ToArgb()] = TextoSecundario,
        [Color.FromArgb(203, 213, 225).ToArgb()] = TextoSecundario,
        [Color.FromArgb(199, 70, 52).ToArgb()] = Acento,
        [Color.FromArgb(239, 104, 84).ToArgb()] = AcentoTexto,
        [SystemColors.Control.ToArgb()] = Superficie,
        [SystemColors.Window.ToArgb()] = SuperficieAlta,
        [SystemColors.ControlText.ToArgb()] = Texto,
        [SystemColors.WindowText.ToArgb()] = Texto,
    };

    private static readonly ConditionalWeakTable<Control, object> Aplicados = new();

    /// <summary>Barra de título oscura (Windows 10 20H1+ y Windows 11) y tema para toda la ventana.</summary>
    public static void AplicarVentana(Form ventana)
    {
        ventana.BackColor = Fondo;
        ventana.ForeColor = Texto;
        ventana.Font = Fuente;
        void Oscura() { int si = 1; _ = DwmSetWindowAttribute(ventana.Handle, 20, ref si, sizeof(int)); }
        if (ventana.IsHandleCreated) Oscura(); else ventana.HandleCreated += (_, _) => Oscura();
        Aplicar(ventana);
    }

    /// <summary>
    /// Pone el tema a un control y a todos sus hijos, incluidos los que se agreguen después.
    /// Se llama solo desde <see cref="VentanaBase"/> y <see cref="FrmPrincipal"/>; no hace falta llamarlo en cada pantalla.
    /// </summary>
    public static void Aplicar(Control control)
    {
        if (Aplicados.TryGetValue(control, out _)) return;
        Aplicados.Add(control, new object());

        Recolorear(control);
        switch (control)
        {
            case BotonPlano:
                break;
            case Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = BordeFuerte;
                b.FlatAppearance.MouseOverBackColor = Borde;
                if (b.BackColor != Acento) { b.BackColor = SuperficieAlta; b.ForeColor = Texto; }
                break;
            case TextBox t:
                t.BackColor = t.ReadOnly ? Fondo : SuperficieAlta;
                t.ForeColor = Texto;
                t.BorderStyle = BorderStyle.FixedSingle;
                ScrollOscuro(t);
                break;
            case ComboBox c:
                c.FlatStyle = FlatStyle.Flat;
                c.BackColor = SuperficieAlta;
                c.ForeColor = Texto;
                ScrollOscuro(c, "DarkMode_CFD");
                if (c.DropDownStyle == ComboBoxStyle.DropDownList && c.DrawMode == DrawMode.Normal)
                {
                    // La lista desplegable de Windows ignora BackColor: se dibuja a mano.
                    c.DrawMode = DrawMode.OwnerDrawFixed;
                    c.ItemHeight = 22;
                    c.DrawItem += DibujarItemCombo;
                }
                break;
            case NumericUpDown n:
                n.BackColor = SuperficieAlta;
                n.ForeColor = Texto;
                n.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ListBox l:
                l.BackColor = SuperficieAlta;
                l.ForeColor = Texto;
                ScrollOscuro(l);
                break;
            case CheckBox chk:
                chk.ForeColor = Texto;
                chk.FlatStyle = FlatStyle.Flat;
                chk.FlatAppearance.BorderColor = BordeFuerte;
                chk.FlatAppearance.CheckedBackColor = Acento;
                chk.FlatAppearance.MouseOverBackColor = Borde;
                break;
            case RadioButton rdo:
                rdo.ForeColor = Texto;
                rdo.FlatStyle = FlatStyle.Flat;
                rdo.FlatAppearance.BorderColor = BordeFuerte;
                rdo.FlatAppearance.CheckedBackColor = Acento;
                break;
            case DataGridView g:
                AplicarGrilla(g);
                break;
        }

        foreach (Control hijo in control.Controls)
            Aplicar(hijo);
        control.ControlAdded += (_, e) => { if (e.Control is not null) Aplicar(e.Control); };
    }

    private static void DibujarItemCombo(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox c) return;
        var seleccionado = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var fondo = new SolidBrush(seleccionado ? AcentoSuave : SuperficieAlta))
            e.Graphics.FillRectangle(fondo, e.Bounds);
        if (e.Index >= 0)
            TextRenderer.DrawText(e.Graphics, c.GetItemText(c.Items[e.Index]), c.Font, Rectangle.Inflate(e.Bounds, -4, 0),
                c.Enabled ? Texto : TextoSecundario, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private static void Recolorear(Control c)
    {
        if (Equivalentes.TryGetValue(c.BackColor.ToArgb(), out var fondo) && c is not Label) c.BackColor = fondo;
        if (Equivalentes.TryGetValue(c.ForeColor.ToArgb(), out var texto)) c.ForeColor = texto;
    }

    /// <summary>Estilo estándar para todas las grillas de resultados.</summary>
    public static void AplicarGrilla(DataGridView grilla)
    {
        grilla.BackgroundColor = Superficie;
        grilla.BorderStyle = BorderStyle.None;
        grilla.AllowUserToAddRows = false;
        grilla.AllowUserToDeleteRows = false;
        grilla.AllowUserToResizeRows = false;
        grilla.ReadOnly = true;
        grilla.RowHeadersVisible = false;
        grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        if (grilla.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.None)
            grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        grilla.EnableHeadersVisualStyles = false;
        grilla.ColumnHeadersDefaultCellStyle.BackColor = SuperficieAlta;
        grilla.ColumnHeadersDefaultCellStyle.ForeColor = TextoSecundario;
        grilla.ColumnHeadersDefaultCellStyle.Font = FuenteNegrita;
        grilla.ColumnHeadersDefaultCellStyle.SelectionBackColor = SuperficieAlta;
        grilla.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextoSecundario;
        grilla.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        grilla.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grilla.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grilla.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grilla.ColumnHeadersHeight = 38;
        grilla.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grilla.DefaultCellStyle.Font = Fuente;
        grilla.DefaultCellStyle.BackColor = Superficie;
        grilla.DefaultCellStyle.ForeColor = Texto;
        grilla.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        grilla.DefaultCellStyle.SelectionBackColor = AcentoSuave;
        grilla.DefaultCellStyle.SelectionForeColor = Texto;
        grilla.GridColor = Borde;
        if (grilla.RowTemplate.Height < 32) grilla.RowTemplate.Height = 32;
        ScrollOscuro(grilla);
        foreach (var barra in grilla.Controls.OfType<ScrollBar>()) ScrollOscuro(barra);

        if (!Aplicados.TryGetValue(grilla, out _)) Aplicados.Add(grilla, new object());
        if (EstadosGrilla.TryGetValue(grilla, out _)) return;
        EstadosGrilla.Add(grilla, new EstadoGrilla());
        // Al cargar datos no queda ninguna fila marcada.
        grilla.DataBindingComplete += (_, _) => grilla.ClearSelection();
        grilla.Paint += PintarVacio;
    }

    // ---------------------------------------------------------------- pastillas de estado

    /// <summary>
    /// Dibuja las celdas de <paramref name="columna"/> como pastillas de color según su texto
    /// (OPEN, LOCKED, COMPLETADO, ERROR, ONLINE, YES…; ver <see cref="Estados"/>).
    /// </summary>
    public static void ColumnaEstado(DataGridView grilla, string columna, Func<string, TipoEstado>? clasificar = null)
    {
        grilla.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null || grilla.Columns[e.ColumnIndex].Name != columna) return;
            if (e.Value is not string texto || texto.Length == 0) return;
            e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
            PintarPastilla(e.Graphics, e.CellBounds, texto, (clasificar ?? Estados.Clasificar)(texto));
            e.Handled = true;
        };
    }

    public static (Color Texto, Color Fondo) ColoresDe(TipoEstado tipo) => tipo switch
    {
        TipoEstado.Ok => (Ok, OkFondo),
        TipoEstado.Advertencia => (Advertencia, AdvertenciaFondo),
        TipoEstado.Peligro => (Peligro, PeligroFondo),
        TipoEstado.Info => (Info, InfoFondo),
        TipoEstado.Acento => (AcentoTexto, AcentoSuave),
        _ => (TextoSecundario, SuperficieAlta)
    };

    public static void PintarPastilla(Graphics g, Rectangle celda, string texto, TipoEstado tipo)
    {
        var (colorTexto, colorFondo) = ColoresDe(tipo);
        var medida = TextRenderer.MeasureText(g, texto, FuentePequena, Size.Empty, TextFormatFlags.NoPadding);
        var alto = 22;
        var r = new Rectangle(celda.X + 8, celda.Y + (celda.Height - alto) / 2, medida.Width + 18, alto);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var ruta = Redondeado(r, alto / 2))
        using (var pincel = new SolidBrush(colorFondo))
            g.FillPath(pincel, ruta);
        TextRenderer.DrawText(g, texto, FuentePequena, r, colorTexto, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    // ---------------------------------------------------------------- estados vacío y cargando

    private sealed class EstadoGrilla
    {
        public string Vacio = "No hay datos para mostrar";
        public string Icono = "";
        public bool Cargando;
    }

    private static readonly ConditionalWeakTable<DataGridView, EstadoGrilla> EstadosGrilla = new();

    /// <summary>Mensaje (con ícono de Segoe MDL2 Assets) que se muestra cuando la grilla no tiene filas.</summary>
    public static void MensajeVacio(DataGridView grilla, string mensaje, string icono = "")
    {
        AplicarGrilla(grilla);
        var estado = EstadosGrilla.GetOrCreateValue(grilla);
        estado.Vacio = mensaje;
        estado.Icono = icono;
        grilla.Invalidate();
    }

    /// <summary>Muestra «Cargando…» sobre la grilla vacía mientras corre una consulta.</summary>
    public static void Cargando(DataGridView grilla, bool cargando)
    {
        EstadosGrilla.GetOrCreateValue(grilla).Cargando = cargando;
        grilla.Invalidate();
    }

    private static void PintarVacio(object? sender, PaintEventArgs e)
    {
        if (sender is not DataGridView g || g.Rows.Count > 0 || !EstadosGrilla.TryGetValue(g, out var estado)) return;
        var arriba = g.ColumnHeadersVisible && g.Columns.Count > 0 ? g.ColumnHeadersHeight : 0;
        var area = new Rectangle(0, arriba, g.ClientSize.Width, g.ClientSize.Height - arriba);
        if (area.Height < 60) return;
        var (icono, texto) = estado.Cargando ? ("", "Cargando…") : (estado.Icono, estado.Vacio);
        using var fuenteIcono = new Font("Segoe MDL2 Assets", 24f);
        var centro = area.Y + area.Height / 2;
        TextRenderer.DrawText(e.Graphics, icono, fuenteIcono, new Rectangle(area.X, centro - 50, area.Width, 40), BordeFuerte,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, texto, Fuente, new Rectangle(area.X, centro - 4, area.Width, 24), TextoSecundario,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    // ---------------------------------------------------------------- utilidades de dibujo

    public static GraphicsPath Redondeado(Rectangle r, int radio)
    {
        var d = Math.Max(1, Math.Min(radio * 2, Math.Min(r.Width, r.Height)));
        var ruta = new GraphicsPath();
        ruta.AddArc(r.X, r.Y, d, d, 180, 90);
        ruta.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        ruta.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        ruta.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        ruta.CloseFigure();
        return ruta;
    }

    /// <summary>Barras de desplazamiento oscuras (tema nativo de Windows 10/11).</summary>
    private static void ScrollOscuro(Control c, string tema = "DarkMode_Explorer")
    {
        void Aplicar() => _ = SetWindowTheme(c.Handle, tema, null);
        if (c.IsHandleCreated) Aplicar(); else c.HandleCreated += (_, _) => Aplicar();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamano);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
}

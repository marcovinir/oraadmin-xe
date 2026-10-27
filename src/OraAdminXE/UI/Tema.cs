namespace OraAdminXE.UI;

/// <summary>
/// Colores y fuentes de la app. Usar SIEMPRE estos valores en las pantallas para que
/// todo se vea consistente (es parte del 20 % de «ventanas o pantallas»).
/// </summary>
public static class Tema
{
    // Menú lateral
    public static readonly Color MenuFondo       = Color.FromArgb(30, 41, 59);    // slate-800
    public static readonly Color MenuTexto       = Color.FromArgb(203, 213, 225); // slate-300
    public static readonly Color MenuHover       = Color.FromArgb(51, 65, 85);    // slate-700
    public static readonly Color MenuActivo      = Color.FromArgb(199, 70, 52);   // rojo Oracle

    // Contenido
    public static readonly Color Fondo           = Color.FromArgb(248, 250, 252); // slate-50
    public static readonly Color Superficie      = Color.White;
    public static readonly Color Texto           = Color.FromArgb(15, 23, 42);    // slate-900
    public static readonly Color TextoSecundario = Color.FromArgb(100, 116, 139); // slate-500
    public static readonly Color Borde           = Color.FromArgb(226, 232, 240); // slate-200
    public static readonly Color Acento          = Color.FromArgb(199, 70, 52);

    // Semáforo (tablespaces, dashboard, estados de respaldos)
    public static readonly Color Ok              = Color.FromArgb(22, 163, 74);
    public static readonly Color Advertencia     = Color.FromArgb(217, 119, 6);
    public static readonly Color Peligro         = Color.FromArgb(220, 38, 38);

    public static readonly Font Fuente           = new("Segoe UI", 10f);
    public static readonly Font FuenteTitulo     = new("Segoe UI Semibold", 16f);
    public static readonly Font FuenteSubtitulo  = new("Segoe UI", 10f, FontStyle.Italic);
    public static readonly Font FuenteMenu       = new("Segoe UI", 10.5f);
    public static readonly Font FuenteCodigo     = new("Cascadia Mono", 10f);
    public static readonly Font FuenteIconos     = new("Segoe MDL2 Assets", 12f);

    /// <summary>Estilo estándar para todas las grillas de resultados.</summary>
    public static void AplicarGrilla(DataGridView grilla)
    {
        grilla.BackgroundColor = Superficie;
        grilla.BorderStyle = BorderStyle.None;
        grilla.AllowUserToAddRows = false;
        grilla.AllowUserToDeleteRows = false;
        grilla.ReadOnly = true;
        grilla.RowHeadersVisible = false;
        grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        grilla.EnableHeadersVisualStyles = false;
        grilla.ColumnHeadersDefaultCellStyle.BackColor = Fondo;
        grilla.ColumnHeadersDefaultCellStyle.ForeColor = Texto;
        grilla.ColumnHeadersDefaultCellStyle.Font = new Font(Fuente, FontStyle.Bold);
        grilla.DefaultCellStyle.Font = Fuente;
        grilla.DefaultCellStyle.SelectionBackColor = Color.FromArgb(254, 226, 226);
        grilla.DefaultCellStyle.SelectionForeColor = Texto;
        grilla.GridColor = Borde;
    }
}

using System.Runtime.InteropServices;

namespace OraAdminXE.UI;

/// <summary>
/// Aviso breve que aparece abajo a la derecha de la ventana y se va solo (no hay que hacer clic en Aceptar).
/// Se usa a través de <see cref="Dialogos.Exito"/>.
/// </summary>
public sealed class Toast : Form
{
    private const int Duracion = 3200;
    private readonly System.Windows.Forms.Timer _temporizador = new() { Interval = Duracion };

    private Toast(string mensaje, TipoEstado tipo)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Tema.SuperficieAlta;
        Font = Tema.Fuente;
        Padding = new Padding(1);

        var (color, _) = Tema.ColoresDe(tipo);
        var icono = tipo == TipoEstado.Peligro ? "" : tipo == TipoEstado.Advertencia ? "" : "";
        var ancho = Math.Min(420, TextRenderer.MeasureText(mensaje, Tema.Fuente).Width + 72);
        var alto = Math.Max(48, TextRenderer.MeasureText(mensaje, Tema.Fuente, new Size(ancho - 60, 0), TextFormatFlags.WordBreak).Height + 26);
        ClientSize = new Size(ancho, alto);

        Controls.Add(new Label
        {
            Text = mensaje, ForeColor = Tema.Texto, Location = new Point(44, 0), Size = new Size(ancho - 56, alto),
            TextAlign = ContentAlignment.MiddleLeft
        });
        Paint += (_, e) =>
        {
            TextRenderer.DrawText(e.Graphics, icono, Tema.FuenteIconos, new Rectangle(14, 0, 22, alto), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            using var lapiz = new Pen(Tema.BordeFuerte);
            e.Graphics.DrawRectangle(lapiz, 0, 0, Width - 1, Height - 1);
            using var franja = new SolidBrush(color);
            e.Graphics.FillRectangle(franja, 0, 0, 3, Height);
        };
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 12, 12));
        _temporizador.Tick += (_, _) => Close();
        Click += (_, _) => Close();
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>Muestra el aviso sobre la ventana de <paramref name="duenio"/> sin quitarle el foco.</summary>
    public static void Mostrar(IWin32Window? duenio, string mensaje, TipoEstado tipo = TipoEstado.Ok)
    {
        var ventana = (duenio as Control)?.FindForm() ?? Form.ActiveForm;
        var toast = new Toast(mensaje, tipo);
        if (ventana is not null)
        {
            var area = ventana.RectangleToScreen(ventana.ClientRectangle);
            toast.Location = new Point(area.Right - toast.Width - 24, area.Bottom - toast.Height - 24);
            toast.Show(ventana);
        }
        else
        {
            var pantalla = Screen.PrimaryScreen!.WorkingArea;
            toast.Location = new Point(pantalla.Right - toast.Width - 24, pantalla.Bottom - toast.Height - 24);
            toast.Show();
        }
        toast._temporizador.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _temporizador.Dispose();
        base.Dispose(disposing);
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int izq, int arriba, int der, int abajo, int anchoElipse, int altoElipse);
}

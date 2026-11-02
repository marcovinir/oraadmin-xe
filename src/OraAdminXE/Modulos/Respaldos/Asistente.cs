using System.Drawing.Drawing2D;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>Piezas visuales de los asistentes de Respaldos y Recuperación (pasos numerados, listas con marco y enlaces).</summary>
public static class Asistente
{
    /// <summary>Encabezado de un paso: número en un círculo, título y una explicación corta.</summary>
    public static Control Seccion(string numero, string titulo, string detalle)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 46 };
        panel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var circulo = new SolidBrush(Tema.AcentoSuave)) g.FillEllipse(circulo, 0, 10, 24, 24);
            TextRenderer.DrawText(g, numero, Tema.FuenteNegrita, new Rectangle(0, 10, 24, 24), Tema.AcentoTexto,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, titulo, Tema.FuenteNegrita, new Point(34, 12), Tema.Texto);
            var ancho = TextRenderer.MeasureText(titulo, Tema.FuenteNegrita).Width;
            TextRenderer.DrawText(g, detalle, Tema.Fuente, new Point(34 + ancho + 6, 12), Tema.TextoSecundario);
        };
        return panel;
    }

    public static Control Espacio(int alto) => new Panel { Dock = DockStyle.Top, Height = alto };

    /// <summary>Borde fino alrededor de una lista (el CheckedListBox sin borde se ve mejor en oscuro).</summary>
    public static Control Marco(Control lista)
    {
        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Tema.BordeFuerte };
        var interior = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 4, 4), BackColor = Tema.SuperficieAlta };
        interior.Controls.Add(lista);
        marco.Controls.Add(interior);
        return marco;
    }

    public static LinkLabel Enlace(string texto, Action accion)
    {
        var enlace = new LinkLabel
        {
            Text = texto, AutoSize = true, LinkColor = Tema.AcentoTexto, ActiveLinkColor = Tema.AcentoHover,
            LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 8, 16, 0)
        };
        enlace.LinkClicked += (_, _) => accion();
        return enlace;
    }

    /// <summary>Línea divisoria a la izquierda (columna derecha) o arriba (pie) de un panel.</summary>
    public static void Divisoria(Panel panel, bool arriba)
    {
        panel.Paint += (_, e) =>
        {
            using var lapiz = new Pen(Tema.Borde);
            if (arriba) e.Graphics.DrawLine(lapiz, 0, 0, panel.Width, 0);
            else e.Graphics.DrawLine(lapiz, 0, 0, 0, panel.Height);
        };
    }
}

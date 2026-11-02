using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace OraAdminXE.UI;

public enum EstiloBoton { Primario, Secundario, Peligro, Fantasma }

/// <summary>
/// Botón plano con esquinas redondeadas, hover e ícono opcional (glifo de Segoe MDL2 Assets).
/// Se usa en el diseñador como cualquier Button; el estilo se elige con <see cref="Estilo"/>.
/// </summary>
public class BotonPlano : Button
{
    private bool _encima;
    private EstiloBoton _estilo = EstiloBoton.Secundario;
    private string? _icono;

    public BotonPlano()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = Tema.Fuente;
        Cursor = Cursors.Hand;
        AutoSize = true;
        MinimumSize = new Size(0, 36);
        Padding = new Padding(12, 0, 12, 0);
        Margin = new Padding(0, 4, 8, 4);
    }

    [Category("Apariencia"), DefaultValue(EstiloBoton.Secundario)]
    public EstiloBoton Estilo { get => _estilo; set { _estilo = value; Invalidate(); } }

    /// <summary>Glifo de Segoe MDL2 Assets, por ejemplo "" (+), "" (borrar), "" (refrescar).</summary>
    [Category("Apariencia"), DefaultValue(null)]
    public string? Icono { get => _icono; set { _icono = value; Invalidate(); } }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var texto = TextRenderer.MeasureText(Text, Font);
        var ancho = texto.Width + Padding.Horizontal + (string.IsNullOrEmpty(_icono) ? 0 : 24);
        return new Size(Math.Max(ancho, MinimumSize.Width), Math.Max(36, MinimumSize.Height));
    }

    protected override void OnMouseEnter(EventArgs e) { _encima = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _encima = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Tema.Fondo);

        var (fondo, borde, texto) = Colores();
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var ruta = Tema.Redondeado(r, 8))
        {
            using (var pincel = new SolidBrush(fondo)) g.FillPath(pincel, ruta);
            if (borde != Color.Empty)
                using (var lapiz = new Pen(borde)) g.DrawPath(lapiz, ruta);
        }
        if (Focused && ShowFocusCues)
            using (var foco = new Pen(Tema.AcentoTexto, 1.5f))
            using (var ruta = Tema.Redondeado(Rectangle.Inflate(r, -2, -2), 6))
                g.DrawPath(foco, ruta);

        var contenido = Rectangle.Inflate(ClientRectangle, -Padding.Left / 2, 0);
        if (!string.IsNullOrEmpty(_icono))
        {
            var anchoTexto = TextRenderer.MeasureText(Text, Font).Width;
            var inicio = (Width - anchoTexto - 22) / 2;
            TextRenderer.DrawText(g, _icono, Tema.FuenteIconos, new Rectangle(inicio, 0, 18, Height), texto,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            contenido = new Rectangle(inicio + 22, 0, anchoTexto + 4, Height);
            TextRenderer.DrawText(g, Text, Font, contenido, texto, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        else
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, texto, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }

    private (Color Fondo, Color Borde, Color Texto) Colores()
    {
        if (!Enabled)
            return (_estilo == EstiloBoton.Fantasma ? Parent?.BackColor ?? Tema.Fondo : Tema.Superficie, Tema.Borde, Tema.BordeFuerte);
        return _estilo switch
        {
            EstiloBoton.Primario => (_encima ? Tema.AcentoHover : Tema.Acento, Color.Empty, Color.White),
            EstiloBoton.Peligro => (_encima ? Tema.PeligroFondo : Tema.SuperficieAlta, _encima ? Tema.Peligro : Color.FromArgb(70, 32, 38), Tema.Peligro),
            EstiloBoton.Fantasma => (_encima ? Tema.SuperficieAlta : Parent?.BackColor ?? Tema.Fondo, Color.Empty, Tema.Texto),
            _ => (_encima ? Tema.Borde : Tema.SuperficieAlta, Tema.BordeFuerte, Tema.Texto)
        };
    }
}

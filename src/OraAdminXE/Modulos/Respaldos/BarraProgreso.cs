using System.ComponentModel;
using System.Drawing.Drawing2D;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Barra de progreso con el tema oscuro (la ProgressBar de Windows siempre es clara).
/// Sin porcentaje se anima como «marquee»; con <see cref="Porcentaje"/> se llena hasta ese valor.
/// La usan Respaldos y Recuperación mientras corre un job de Data Pump.
/// </summary>
public sealed class BarraProgreso : Control
{
    private readonly System.Windows.Forms.Timer _animacion = new() { Interval = 30 };
    private int? _porcentaje;
    private float _posicion;

    public BarraProgreso()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 8;
        _animacion.Tick += (_, _) => { _posicion = (_posicion + 0.012f) % 1.3f; Invalidate(); };
    }

    /// <summary>null = indeterminado (animado); 0–100 = avance real.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? Porcentaje
    {
        get => _porcentaje;
        set { _porcentaje = value is null ? null : Math.Clamp(value.Value, 0, 100); Invalidate(); }
    }

    /// <summary>Muestra la barra y arranca la animación (o la detiene y la oculta).</summary>
    public void Iniciar() { Porcentaje = null; Visible = true; _animacion.Start(); }

    public void Detener() { _animacion.Stop(); Visible = false; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Tema.Fondo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var pista = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var ruta = Tema.Redondeado(pista, Height / 2))
        using (var fondo = new SolidBrush(Tema.SuperficieAlta))
            g.FillPath(fondo, ruta);

        Rectangle lleno;
        if (_porcentaje is { } pct)
            lleno = new Rectangle(0, 0, Math.Max(Height, pista.Width * pct / 100), pista.Height);
        else
        {
            var ancho = pista.Width * 0.3f;
            var x = (int)(pista.Width * _posicion - ancho);
            lleno = Rectangle.Intersect(pista, new Rectangle(x, 0, (int)ancho, pista.Height));
        }
        if (lleno.Width <= 0) return;
        using var relleno = Tema.Redondeado(lleno, Height / 2);
        using var color = new SolidBrush(Tema.Acento);
        g.FillPath(color, relleno);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animacion.Dispose();
        base.Dispose(disposing);
    }
}

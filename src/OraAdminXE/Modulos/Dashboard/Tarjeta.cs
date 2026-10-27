using System.ComponentModel;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Dashboard;

/// <summary>Tarjeta de indicador: título, valor grande, detalle y una franja de color a la izquierda.</summary>
public sealed class Tarjeta : Panel
{
    private readonly Label _lblTitulo = new() { AutoSize = true, ForeColor = Tema.TextoSecundario, Font = new Font("Segoe UI", 9f), Location = new Point(18, 14) };
    private readonly Label _lblValor = new() { AutoSize = true, ForeColor = Tema.Texto, Font = new Font("Segoe UI Semibold", 18f), Location = new Point(16, 34) };
    private readonly Label _lblDetalle = new() { AutoSize = true, ForeColor = Tema.TextoSecundario, Font = new Font("Segoe UI", 9f), Location = new Point(18, 76) };
    private Color _acento = Tema.Borde;

    public Tarjeta()
    {
        BackColor = Tema.Superficie;
        Margin = new Padding(0, 0, 12, 0);
        DoubleBuffered = true;
        Controls.AddRange([_lblTitulo, _lblValor, _lblDetalle]);
    }

    [Category("Tarjeta"), DefaultValue("")]
    public string Titulo { get => _lblTitulo.Text; set => _lblTitulo.Text = value.ToUpperInvariant(); }

    [Category("Tarjeta"), DefaultValue("")]
    public string Valor { get => _lblValor.Text; set => _lblValor.Text = value; }

    [Category("Tarjeta"), DefaultValue("")]
    public string Detalle { get => _lblDetalle.Text; set => _lblDetalle.Text = value; }

    /// <summary>Color de la franja izquierda (estado: Ok, Advertencia, Peligro…).</summary>
    [Category("Tarjeta")]
    public Color Acento { get => _acento; set { _acento = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borde = new Pen(Tema.Borde);
        e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
        using var franja = new SolidBrush(_acento);
        e.Graphics.FillRectangle(franja, 0, 0, 5, Height);
    }
}

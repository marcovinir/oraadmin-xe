namespace OraAdminXE.UI;

/// <summary>
/// Pestañas planas (subrayado del color de acento) que reemplazan al TabControl de Windows, que no admite tema oscuro.
/// Uso: <c>pestanas.Agregar("Usuarios", new UcUsuarios());</c>
/// </summary>
public sealed class Pestanas : Panel
{
    private readonly FlowLayoutPanel _encabezado = new() { Dock = DockStyle.Top, Height = 44, WrapContents = false, Padding = new Padding(4, 0, 0, 0) };
    private readonly Panel _contenido = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 1, 0, 0) };
    private readonly List<(Label Titulo, Control Contenido)> _pestanas = [];
    private int _seleccionada = -1;

    public event EventHandler? SeleccionCambiada;

    public Pestanas()
    {
        BackColor = Tema.Fondo;
        _contenido.BackColor = Tema.Superficie;
        _encabezado.BackColor = Tema.Fondo;
        _encabezado.Paint += (_, e) =>
        {
            using var lapiz = new Pen(Tema.Borde);
            e.Graphics.DrawLine(lapiz, 0, _encabezado.Height - 1, _encabezado.Width, _encabezado.Height - 1);
        };
        Controls.Add(_contenido);
        Controls.Add(_encabezado);
    }

    public int Seleccionada => _seleccionada;
    public Control? ContenidoSeleccionado => _seleccionada >= 0 ? _pestanas[_seleccionada].Contenido : null;

    public void Agregar(string titulo, Control contenido)
    {
        var etiqueta = new Label
        {
            Text = titulo, AutoSize = false, Height = 44, Font = Tema.FuenteNegrita, Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 4, 0),
            Width = TextRenderer.MeasureText(titulo, Tema.FuenteNegrita).Width + 32
        };
        var indice = _pestanas.Count;
        etiqueta.Click += (_, _) => Seleccionar(indice);
        etiqueta.MouseEnter += (_, _) => { if (indice != _seleccionada) etiqueta.ForeColor = Tema.Texto; };
        etiqueta.MouseLeave += (_, _) => { if (indice != _seleccionada) etiqueta.ForeColor = Tema.TextoSecundario; };
        etiqueta.Paint += (_, e) =>
        {
            if (indice != _seleccionada) return;
            using var pincel = new SolidBrush(Tema.Acento);
            e.Graphics.FillRectangle(pincel, 10, etiqueta.Height - 3, etiqueta.Width - 20, 3);
        };
        contenido.Dock = DockStyle.Fill;
        contenido.Visible = false;
        _contenido.Controls.Add(contenido);
        _encabezado.Controls.Add(etiqueta);
        _pestanas.Add((etiqueta, contenido));
        if (_seleccionada < 0) Seleccionar(0, avisar: false);
        else Pintar();
    }

    public void Seleccionar(int indice, bool avisar = true)
    {
        if (indice == _seleccionada || indice < 0 || indice >= _pestanas.Count) return;
        _seleccionada = indice;
        for (var i = 0; i < _pestanas.Count; i++)
            _pestanas[i].Contenido.Visible = i == indice;
        Pintar();
        if (avisar) SeleccionCambiada?.Invoke(this, EventArgs.Empty);
    }

    private void Pintar()
    {
        for (var i = 0; i < _pestanas.Count; i++)
        {
            _pestanas[i].Titulo.ForeColor = i == _seleccionada ? Tema.Texto : Tema.TextoSecundario;
            _pestanas[i].Titulo.Invalidate();
        }
    }
}

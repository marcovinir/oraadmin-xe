using System.Drawing.Drawing2D;
using OraAdminXE.Datos;
using OraAdminXE.Modulos;

namespace OraAdminXE.UI;

/// <summary>
/// Ventana principal: menú lateral (logo, módulos y conexión activa) y el panel donde se muestra el módulo activo.
/// Los módulos se registran en <see cref="RegistroModulos"/>; aquí no hay que tocar nada para agregar uno.
/// </summary>
public sealed class FrmPrincipal : VentanaBase
{
    private readonly Panel _contenido = new() { Dock = DockStyle.Fill, BackColor = Tema.Fondo };
    private readonly Dictionary<string, ModuloBase> _abiertos = new();
    private readonly List<ItemMenu> _items = [];
    private ItemMenu? _activo;
    private ModuloBase? _moduloActivo;

    public FrmPrincipal()
    {
        Text = "OraAdmin XE — Consola DBA para Oracle 21c XE";
        MinimumSize = new Size(1100, 700);
        Size = new Size(1366, 820);
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_contenido);
        Controls.Add(CrearMenu());

        Shown += async (_, _) => await AbrirAsync(_items[0]);
        FormClosing += (_, _) => _moduloActivo?.AlDesactivar();
    }

    private Control CrearMenu()
    {
        var menu = new Panel { Dock = DockStyle.Left, Width = 236, BackColor = Tema.MenuFondo };
        menu.Paint += (_, e) =>
        {
            using var lapiz = new Pen(Tema.Borde);
            e.Graphics.DrawLine(lapiz, menu.Width - 1, 0, menu.Width - 1, menu.Height);
        };

        var lista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 13, 0), BackColor = Tema.MenuFondo };
        // Dock=Top apila al revés: se agregan del último al primero.
        foreach (var entrada in RegistroModulos.Todos.Reverse())
        {
            var item = new ItemMenu(entrada) { Dock = DockStyle.Top };
            item.Click += async (_, _) => await AbrirAsync(item);
            lista.Controls.Add(item);
            _items.Insert(0, item);
        }

        menu.Controls.Add(lista);
        menu.Controls.Add(CrearConexion());
        menu.Controls.Add(CrearLogo());
        return menu;
    }

    private static Control CrearLogo()
    {
        var logo = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Tema.MenuFondo };
        logo.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ruta = Tema.Redondeado(new Rectangle(22, 22, 32, 32), 8))
            using (var pincel = new SolidBrush(Tema.Acento))
                g.FillPath(pincel, ruta);
            TextRenderer.DrawText(g, "XE", Tema.FuenteNegrita, new Rectangle(22, 22, 32, 32), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "OraAdmin", Tema.FuenteLogo, new Point(62, 21), Tema.Texto);
            TextRenderer.DrawText(g, "Oracle 21c XE", Tema.FuentePequena, new Point(64, 43), Tema.TextoSecundario);
        };
        return logo;
    }

    private static Control CrearConexion()
    {
        var conexion = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Tema.MenuFondo };
        var conectado = !string.IsNullOrEmpty(Sesion.Bd.Usuario);
        conexion.Paint += (_, e) =>
        {
            var g = e.Graphics;
            using (var lapiz = new Pen(Tema.Borde)) g.DrawLine(lapiz, 12, 0, conexion.Width - 13, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var punto = new SolidBrush(conectado ? Tema.Ok : Tema.Peligro)) g.FillEllipse(punto, 24, 19, 8, 8);
            TextRenderer.DrawText(g, conectado ? Sesion.Bd.Usuario : "Sin conexión", Tema.FuenteNegrita, new Point(40, 14), Tema.Texto);
            if (conectado)
                TextRenderer.DrawText(g, Sesion.Bd.Config.DataSource, Tema.FuentePequena, new Point(40, 35), Tema.TextoSecundario);
        };
        return conexion;
    }

    private async Task AbrirAsync(ItemMenu item)
    {
        if (_activo is not null) _activo.Activo = false;
        item.Activo = true;
        _activo = item;

        _moduloActivo?.AlDesactivar();

        var entrada = item.Entrada;
        if (!_abiertos.TryGetValue(entrada.Titulo, out var modulo))
        {
            modulo = entrada.Crear();
            _abiertos[entrada.Titulo] = modulo;
        }

        _contenido.SuspendLayout();
        _contenido.Controls.Clear();
        _contenido.Controls.Add(modulo);
        _contenido.ResumeLayout();
        _moduloActivo = modulo;

        try
        {
            await modulo.AlActivarAsync();
        }
        catch (NotImplementedException)
        {
            // Módulo aún en construcción: se muestra su pantalla vacía.
        }
    }

    /// <summary>Opción del menú lateral: ícono, texto y fondo redondeado al pasar el mouse o estar activa.</summary>
    private sealed class ItemMenu : Control
    {
        private bool _encima;
        private bool _activo;

        public ItemMenu(EntradaModulo entrada)
        {
            Entrada = entrada;
            Text = entrada.Titulo;
            Height = 42;
            Cursor = Cursors.Hand;
            Font = Tema.FuenteMenu;
            BackColor = Tema.MenuFondo;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public EntradaModulo Entrada { get; }

        public bool Activo { get => _activo; set { _activo = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { _encima = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _encima = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tema.MenuFondo);
            if (_activo || _encima)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var ruta = Tema.Redondeado(new Rectangle(0, 3, Width - 1, Height - 7), 8);
                using var pincel = new SolidBrush(_activo ? Tema.AcentoSuave : Tema.MenuHover);
                g.FillPath(pincel, ruta);
            }
            var color = _activo ? Tema.AcentoTexto : _encima ? Tema.Texto : Tema.TextoSecundario;
            TextRenderer.DrawText(g, Entrada.Icono, Tema.FuenteIconos, new Rectangle(8, 0, 28, Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, _activo ? Tema.FuenteMenuActiva : Font, new Rectangle(44, 0, Width - 48, Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }
}

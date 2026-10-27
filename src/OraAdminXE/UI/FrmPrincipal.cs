using OraAdminXE.Datos;
using OraAdminXE.Modulos;

namespace OraAdminXE.UI;

/// <summary>
/// Ventana principal: barra superior, menú lateral y panel donde se muestra el módulo activo.
/// Los módulos se registran en <see cref="RegistroModulos"/>; aquí no hay que tocar nada para agregar uno.
/// </summary>
public sealed class FrmPrincipal : Form
{
    private readonly Panel _menu = new();
    private readonly Panel _contenido = new();
    private readonly Label _lblConexion = new();
    private readonly Dictionary<string, ModuloBase> _abiertos = new();
    private Button? _botonActivo;
    private ModuloBase? _moduloActivo;

    public FrmPrincipal()
    {
        Text = "OraAdmin XE — Consola DBA para Oracle 21c XE";
        Font = Tema.Fuente;
        BackColor = Tema.Fondo;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1366, 820);
        StartPosition = FormStartPosition.CenterScreen;

        _contenido.Dock = DockStyle.Fill;
        _contenido.BackColor = Tema.Fondo;

        Controls.Add(_contenido);
        Controls.Add(CrearMenu());
        Controls.Add(CrearBarraSuperior());

        Shown += async (_, _) => await AbrirAsync(RegistroModulos.Todos[0], (Button)_menu.Controls[^1]);
        FormClosing += (_, _) => _moduloActivo?.AlDesactivar();
    }

    private Control CrearBarraSuperior()
    {
        var barra = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Tema.Superficie };
        barra.Paint += (_, e) => e.Graphics.DrawLine(new Pen(Tema.Borde), 0, barra.Height - 1, barra.Width, barra.Height - 1);

        var titulo = new Label
        {
            Text = "OraAdmin XE",
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = Tema.Acento,
            AutoSize = true,
            Location = new Point(16, 12)
        };

        _lblConexion.AutoSize = true;
        _lblConexion.ForeColor = Tema.TextoSecundario;
        _lblConexion.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _lblConexion.Text = string.IsNullOrEmpty(Sesion.Bd.Usuario)
            ? "Sin conexión"
            : $"● {Sesion.Bd.Usuario} @ {Sesion.Bd.Config.DataSource}";
        barra.Resize += (_, _) => _lblConexion.Location = new Point(barra.Width - _lblConexion.Width - 16, 15);

        barra.Controls.Add(titulo);
        barra.Controls.Add(_lblConexion);
        return barra;
    }

    private Control CrearMenu()
    {
        _menu.Dock = DockStyle.Left;
        _menu.Width = 230;
        _menu.BackColor = Tema.MenuFondo;
        _menu.Padding = new Padding(0, 12, 0, 0);

        // Dock=Top apila al revés: se agregan del último al primero.
        foreach (var entrada in RegistroModulos.Todos.Reverse())
        {
            var boton = new Button
            {
                Text = entrada.Titulo,
                Padding = new Padding(52, 0, 0, 0),
                Dock = DockStyle.Top,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Tema.MenuTexto,
                BackColor = Tema.MenuFondo,
                Font = Tema.FuenteMenu,
                Cursor = Cursors.Hand,
                Tag = entrada
            };
            boton.FlatAppearance.BorderSize = 0;
            boton.FlatAppearance.MouseOverBackColor = Tema.MenuHover;
            // WinForms no dibuja emojis: el ícono se pinta aparte con la fuente de íconos de Windows.
            boton.Paint += (_, e) => TextRenderer.DrawText(e.Graphics, entrada.Icono, Tema.FuenteIconos,
                new Rectangle(18, 0, 24, boton.Height), boton.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            boton.Click += async (_, _) => await AbrirAsync(entrada, boton);
            _menu.Controls.Add(boton);
        }
        return _menu;
    }

    private async Task AbrirAsync(EntradaModulo entrada, Button boton)
    {
        if (_botonActivo is not null)
        {
            _botonActivo.BackColor = Tema.MenuFondo;
            _botonActivo.ForeColor = Tema.MenuTexto;
        }
        boton.BackColor = Tema.MenuActivo;
        boton.ForeColor = Color.White;
        _botonActivo = boton;

        _moduloActivo?.AlDesactivar();

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
}

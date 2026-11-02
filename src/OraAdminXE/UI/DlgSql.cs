namespace OraAdminXE.UI;

/// <summary>
/// Diálogo común para mostrar una sentencia SQL (con o sin pedir confirmación) o el detalle de un error.
/// Se usa a través de <see cref="Dialogos"/>; las pantallas no lo crean directamente.
/// </summary>
public sealed class DlgSql : VentanaBase
{
    public enum Modo { Confirmar, Ver, Error, Aviso }

    private readonly TextBox _txtDetalle = new();
    private Control? _foco;

    public DlgSql(Modo modo, string titulo, string detalle, string? aviso = null, bool destructiva = false)
    {
        Text = modo switch { Modo.Error => "Error", Modo.Aviso => "Revise los datos", _ => titulo };
        Font = Tema.Fuente;
        BackColor = Tema.Superficie;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(520, 300);
        Size = modo == Modo.Aviso ? new Size(560, 260) : new Size(760, modo == Modo.Error ? 340 : 420);

        var colorAviso = modo == Modo.Error || destructiva ? Tema.Peligro : modo == Modo.Aviso ? Tema.Advertencia : Tema.Acento;

        var encabezado = new Panel { Dock = DockStyle.Top, Height = aviso is null ? 56 : 84, Padding = new Padding(20, 14, 20, 6) };
        var lblTitulo = new Label
        {
            Text = titulo,
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = modo switch { Modo.Error => Tema.Peligro, Modo.Aviso => Tema.Advertencia, _ => Tema.Texto },
            AutoSize = true,
            Dock = DockStyle.Top
        };
        encabezado.Controls.Add(lblTitulo);
        if (aviso is not null)
        {
            var lblAviso = new Label { Text = aviso, ForeColor = modo == Modo.Confirmar && !destructiva ? Tema.TextoSecundario : colorAviso, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 0) };
            encabezado.Controls.Add(lblAviso);
            lblAviso.BringToFront();
        }

        _txtDetalle.Multiline = true;
        _txtDetalle.ReadOnly = true;
        _txtDetalle.ScrollBars = ScrollBars.Both;
        _txtDetalle.WordWrap = modo is Modo.Error or Modo.Aviso;
        _txtDetalle.ScrollBars = modo == Modo.Aviso ? ScrollBars.None : ScrollBars.Both;
        _txtDetalle.Font = modo == Modo.Aviso ? Tema.Fuente : Tema.FuenteCodigo;
        _txtDetalle.BackColor = Tema.Fondo;
        _txtDetalle.BorderStyle = BorderStyle.FixedSingle;
        _txtDetalle.Dock = DockStyle.Fill;
        _txtDetalle.Text = detalle.Replace("\r\n", "\n").Replace("\n", "\r\n");

        var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 8) };
        cuerpo.Controls.Add(_txtDetalle);

        var pie = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Tema.Fondo
        };

        if (modo == Modo.Confirmar)
        {
            var btnEjecutar = Boton(destructiva ? "Ejecutar de todos modos" : "Ejecutar", DialogResult.OK,
                destructiva ? EstiloBoton.Peligro : EstiloBoton.Primario, destructiva ? "" : "");
            var btnCancelar = Boton("Cancelar", DialogResult.Cancel);
            pie.Controls.Add(btnEjecutar);
            pie.Controls.Add(btnCancelar);
            // Con acciones destructivas Enter NO ejecuta: hay que hacer clic a propósito.
            AcceptButton = destructiva ? null : btnEjecutar;
            CancelButton = btnCancelar;
            _foco = destructiva ? btnCancelar : btnEjecutar;
        }
        else
        {
            var btnCerrar = Boton("Cerrar", DialogResult.Cancel);
            pie.Controls.Add(btnCerrar);
            AcceptButton = btnCerrar;
            CancelButton = btnCerrar;
            _foco = btnCerrar;
        }

        if (modo != Modo.Aviso)
        {
            var btnCopiar = Boton(modo == Modo.Error ? "Copiar detalle" : "Copiar SQL", DialogResult.None, EstiloBoton.Fantasma, "");
            btnCopiar.Click += (_, _) =>
            {
                Clipboard.SetText(_txtDetalle.Text);
                btnCopiar.Text = "Copiado";
                btnCopiar.Icono = "";
            };
            pie.Controls.Add(btnCopiar);
        }

        Controls.Add(cuerpo);
        Controls.Add(pie);
        Controls.Add(encabezado);

        // El foco se pone al mostrarse: antes de eso los botones aún no están visibles.
        Shown += (_, _) => _foco?.Focus();
    }

    private static BotonPlano Boton(string texto, DialogResult resultado, EstiloBoton estilo = EstiloBoton.Secundario, string? icono = null) => new()
    {
        Text = texto,
        DialogResult = resultado,
        Estilo = estilo,
        Icono = icono,
        MinimumSize = new Size(110, 36),
        Margin = new Padding(8, 0, 0, 0)
    };
}

using OraAdminXE.Datos;

namespace OraAdminXE.UI;

/// <summary>
/// Pantalla de conexión: host, puerto, servicio, usuario y contraseña.
/// Recuerda la última conexión en <c>conexion.json</c> (sin contraseña) y solo deja entrar a usuarios con rol DBA.
/// </summary>
public partial class FrmLogin : Form
{
    public FrmLogin()
    {
        InitializeComponent();
        Cargar(ConfigConexion.Cargar());
        Shown += (_, _) => (txtClave.Text.Length == 0 && txtUsuario.Text.Length > 0 ? txtClave : txtHost).Focus();
    }

    private void Cargar(ConfigConexion config)
    {
        txtHost.Text = config.Host;
        numPuerto.Value = Math.Clamp(config.Puerto, (int)numPuerto.Minimum, (int)numPuerto.Maximum);
        txtServicio.Text = config.Servicio;
        txtUsuario.Text = config.Usuario;
    }

    private ConfigConexion Leer() => new()
    {
        Host = txtHost.Text.Trim(),
        Puerto = (int)numPuerto.Value,
        Servicio = txtServicio.Text.Trim(),
        Usuario = txtUsuario.Text.Trim()
    };

    private async void BtnConectar_Click(object? sender, EventArgs e)
    {
        var config = Leer();
        var faltante = new (string Valor, string Nombre, Control Campo)[]
        {
            (config.Host, "el servidor", txtHost),
            (config.Servicio, "el servicio", txtServicio),
            (config.Usuario, "el usuario", txtUsuario),
            (txtClave.Text, "la contraseña", txtClave)
        }.FirstOrDefault(c => c.Valor.Length == 0);
        if (faltante.Campo is not null)
        {
            MostrarEstado($"Falta {faltante.Nombre}.", Tema.Peligro);
            faltante.Campo.Focus();
            return;
        }

        Ocupado(true);
        MostrarEstado($"Conectando a {config.DataSource}…", Tema.TextoSecundario);
        try
        {
            await Sesion.Bd.ConectarAsync(config, txtClave.Text);
            config.Guardar();
            await Sesion.Bd.RegistrarBitacoraAsync(Modulo.General, "Inicio de sesión", null, true,
                $"{Sesion.Bd.Usuario} @ {config.DataSource}");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MostrarEstado("No se pudo conectar.", Tema.Peligro);
            Dialogos.Error(this, ex);
            Ocupado(false);
            txtClave.SelectAll();
            txtClave.Focus();
        }
    }

    private void Ocupado(bool ocupado)
    {
        foreach (Control c in new Control[] { txtHost, numPuerto, txtServicio, txtUsuario, txtClave, btnConectar })
            c.Enabled = !ocupado;
        UseWaitCursor = ocupado;
    }

    private void MostrarEstado(string texto, Color color)
    {
        lblEstado.Text = texto;
        lblEstado.ForeColor = color;
    }
}

namespace OraAdminXE.UI;

/// <summary>
/// Pantalla de conexión: host, puerto, servicio, usuario y contraseña.
/// PENDIENTE: issue «Núcleo: pantalla de login».
/// Mientras tanto deja entrar sin conexión para poder ver el menú y las pantallas.
/// </summary>
public partial class FrmLogin : Form
{
    public FrmLogin()
    {
        InitializeComponent();
    }

    private void BtnEntrar_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}

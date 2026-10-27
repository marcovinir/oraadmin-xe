namespace OraAdminXE.UI;

/// <summary>
/// Base para TODOS los formularios y diálogos de la app: les pone el tema oscuro
/// (barra de título incluida). En un diálogo nuevo: <c>public partial class DlgAlgo : VentanaBase</c>.
/// </summary>
public class VentanaBase : Form
{
    public VentanaBase()
    {
        Font = Tema.Fuente;
        BackColor = Tema.Fondo;
        ForeColor = Tema.Texto;
    }

    protected override void OnLoad(EventArgs e)
    {
        Tema.AplicarVentana(this);
        base.OnLoad(e);
    }
}

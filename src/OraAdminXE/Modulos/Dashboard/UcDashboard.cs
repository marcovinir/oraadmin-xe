using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Dashboard;

/// <summary>
/// Inicio: Estado de la instancia, sesiones, tablespaces y último respaldo
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcDashboard : ModuloBase
{
    public UcDashboard()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.General;
}

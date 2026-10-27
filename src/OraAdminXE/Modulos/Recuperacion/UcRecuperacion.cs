using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>
/// Recuperación: Restaurar respaldos completos, esquemas o tablas
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcRecuperacion : ModuloBase
{
    public UcRecuperacion()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Recuperacion;
}

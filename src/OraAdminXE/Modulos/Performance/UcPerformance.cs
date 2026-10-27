using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Performance: Sesiones, bloqueos, top SQL, esperas y métricas en vivo
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcPerformance : ModuloBase
{
    public UcPerformance()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Performance;
}

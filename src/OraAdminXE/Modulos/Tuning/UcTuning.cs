using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Tuning de consultas: Planes de ejecución, estadísticas e índices
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcTuning : ModuloBase
{
    public UcTuning()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Tuning;
}

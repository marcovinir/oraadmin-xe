using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Tuning de consultas: Planes de ejecución, estadísticas e índices
/// Pestaña Plan de ejecución (#21, <see cref="UcPlanEjecucion"/>). Estadísticas e Índices: issue #22.
/// </summary>
public partial class UcTuning : ModuloBase
{
    private readonly UcPlanEjecucion _plan = new();

    public UcTuning()
    {
        InitializeComponent();
        pestanas.Agregar("Plan de ejecución", _plan);
    }

    public override Modulo Modulo => Modulo.Tuning;

    public override Task AlActivarAsync() => _plan.CargarAsync();
}

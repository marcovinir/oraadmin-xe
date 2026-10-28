using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>
/// Tuning de consultas: Planes de ejecución, estadísticas e índices
/// Pestaña Plan de ejecución (#21, <see cref="UcPlanEjecucion"/>). Estadísticas (#22, <see cref="UcEstadisticas"/>) e Índices (#22, <see cref="UcIndices"/>).
/// </summary>
public partial class UcTuning : ModuloBase
{
    private readonly UcPlanEjecucion _plan = new();
    private readonly UcEstadisticas _estadisticas = new();
    private readonly UcIndices _indices = new();

    public UcTuning()
    {
        InitializeComponent();
        pestanas.Agregar("Plan de ejecución", _plan);
        pestanas.Agregar("Estadísticas", _estadisticas);
        pestanas.Agregar("Índices", _indices);
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
    }

    public override Modulo Modulo => Modulo.Tuning;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    /// <summary>Carga solo la pestaña visible (cada una consulta lo suyo al abrirse).</summary>
    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _plan.CargarAsync(),
        1 => _estadisticas.CargarAsync(),
        2 => _indices.CargarAsync(),
        _ => Task.CompletedTask
    };
}

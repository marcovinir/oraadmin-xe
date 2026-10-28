using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Performance: Sesiones, bloqueos, top SQL, esperas y métricas en vivo.
/// Pestaña Sesiones y bloqueos (#23, <see cref="UcSesiones"/>).
/// Pestaña Métricas (#24, <see cref="UcMetricas"/>).
/// Esperas y memoria (#25).
/// </summary>
public partial class UcPerformance : ModuloBase
{
    private readonly UcSesiones _sesiones = new();
    private readonly UcMetricas _metricas = new();

    public UcPerformance()
    {
        InitializeComponent();
        pestanas.Agregar("Sesiones y bloqueos", _sesiones);
        pestanas.Agregar("Métricas", _metricas);
        pestanas.SeleccionCambiada += (_, _) => CargarPestana();
    }

    public override Modulo Modulo => Modulo.Performance;

    public override Task AlActivarAsync()
    {
        CargarPestana();
        return Task.CompletedTask;
    }

    public override void AlDesactivar()
    {
        _sesiones.AlDesactivar();
        _metricas.AlDesactivar();
    }

    private void CargarPestana()
    {
        switch (pestanas.Seleccionada)
        {
            case 0:
                _metricas.AlDesactivar();
                _sesiones.AlActivar();
                break;
            case 1:
                _sesiones.AlDesactivar();
                _metricas.AlActivar();
                break;
        }
    }
}

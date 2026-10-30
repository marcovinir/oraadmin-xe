using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Performance: Sesiones, bloqueos, top SQL, esperas y métricas en vivo.
/// Pestaña Sesiones y bloqueos (#23, <see cref="UcSesiones"/>).
/// Pestaña Métricas (#24, <see cref="UcMetricas"/>).
/// Pestaña Esperas y memoria (#25, <see cref="UcEsperasMemoria"/>).
/// </summary>
public partial class UcPerformance : ModuloBase
{
    private readonly UcSesiones _sesiones = new();
    private readonly UcMetricas _metricas = new();
    private readonly UcEsperasMemoria _esperas = new();

    public UcPerformance()
    {
        InitializeComponent();
        pestanas.Agregar("Sesiones y bloqueos", _sesiones);
        pestanas.Agregar("Métricas", _metricas);
        pestanas.Agregar("Esperas y memoria", _esperas);
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
            case 2:
                _sesiones.AlDesactivar();
                _metricas.AlDesactivar();
                _esperas.AlActivar();
                break;
        }
    }
}

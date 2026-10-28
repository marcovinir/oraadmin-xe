using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Performance: Sesiones, bloqueos, top SQL, esperas y métricas en vivo.
/// Pestaña Sesiones y bloqueos (#23, <see cref="UcSesiones"/>). Métricas (#24) y Esperas (#25).
/// </summary>
public partial class UcPerformance : ModuloBase
{
    private readonly UcSesiones _sesiones = new();

    public UcPerformance()
    {
        InitializeComponent();
        pestanas.Agregar("Sesiones y bloqueos", _sesiones);
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
    }

    private void CargarPestana()
    {
        switch (pestanas.Seleccionada)
        {
            case 0:
                _sesiones.AlActivar();
                break;
        }
    }
}

using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>
/// Recuperación de respaldos con DBMS_DATAPUMP: pestañas Restaurar (#13, <see cref="UcRestaurar"/>)
/// e Historial (#14, <see cref="UcHistorialRestauraciones"/>).
/// </summary>
public partial class UcRecuperacion : ModuloBase
{
    private readonly UcRestaurar _restaurar = new();
    private readonly UcHistorialRestauraciones _historial = new();

    public UcRecuperacion()
    {
        InitializeComponent();
        pestanas.Agregar("Restaurar", _restaurar);
        pestanas.Agregar("Historial", _historial);
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
        _restaurar.RestauracionTerminada += async (_, r) => await _historial.CargarAsync(r?.IdRestauracion);
    }

    public override Modulo Modulo => Modulo.Recuperacion;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _restaurar.CargarAsync(),
        1 => _historial.CargarAsync(),
        _ => Task.CompletedTask
    };
}

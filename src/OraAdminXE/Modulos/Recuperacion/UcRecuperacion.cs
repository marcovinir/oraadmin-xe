using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>
/// Recuperación de respaldos con DBMS_DATAPUMP: pestaña Restaurar (#13, <see cref="UcRestaurar"/>).
/// </summary>
public partial class UcRecuperacion : ModuloBase
{
    private readonly UcRestaurar _restaurar = new();

    public UcRecuperacion()
    {
        InitializeComponent();
        pestanas.Agregar("Restaurar", _restaurar);
    }

    public override Modulo Modulo => Modulo.Recuperacion;

    public override Task AlActivarAsync() => _restaurar.CargarAsync();
}

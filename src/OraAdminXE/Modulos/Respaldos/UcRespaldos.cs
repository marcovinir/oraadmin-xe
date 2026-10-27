using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Respaldos: Respaldos Full, por esquema y por tabla con Data Pump
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcRespaldos : ModuloBase
{
    public UcRespaldos()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Respaldos;
}

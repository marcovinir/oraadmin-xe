using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Bitacora;

/// <summary>
/// Bitácora: Historial de todo lo ejecutado desde la app
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcBitacora : ModuloBase
{
    public UcBitacora()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.General;
}

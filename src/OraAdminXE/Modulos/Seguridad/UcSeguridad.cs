using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Seguridad de usuarios: Usuarios, roles, privilegios y perfiles
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcSeguridad : ModuloBase
{
    public UcSeguridad()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Seguridad;
}

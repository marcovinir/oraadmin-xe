using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>
/// Tablespaces: Uso, creación y mantenimiento de tablespaces y datafiles
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcTablespaces : ModuloBase
{
    public UcTablespaces()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Tablespaces;
}

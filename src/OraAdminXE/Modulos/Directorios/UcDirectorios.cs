using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>
/// Directorios y archivos: Objetos DIRECTORY y archivos .dmp/.log en disco
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcDirectorios : ModuloBase
{
    public UcDirectorios()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Directorios;
}

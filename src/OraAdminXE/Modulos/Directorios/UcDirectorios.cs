using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>
/// Directorios y archivos: Objetos DIRECTORY y archivos .dmp/.log en disco
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcDirectorios : ModuloBase
{
    private readonly UcObjetosDirectorios _objetos = new();
    private readonly UcArchivosDirectorios _archivos = new();

    public UcDirectorios()
    {
        InitializeComponent();
        pestanas.Agregar("Objetos DIRECTORY", _objetos);
        pestanas.Agregar("Archivos", _archivos);
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
    }

    public override Modulo Modulo => Modulo.Directorios;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    /// <summary>
    /// El host conserva las pestañas del módulo. La issue #18 agregará aquí la pestaña de archivos
    /// sin acoplar su implementación a la administración de objetos DIRECTORY.
    /// </summary>
    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _objetos.CargarAsync(),
        1 => _archivos.CargarAsync(),
        _ => Task.CompletedTask
    };
}

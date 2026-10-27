using System.ComponentModel;
using OraAdminXE.Datos;

namespace OraAdminXE.UI;

/// <summary>
/// Base de todas las pantallas de módulo. Cada módulo es un UserControl que hereda de esta clase
/// y que <see cref="FrmPrincipal"/> muestra en el panel de contenido al elegirlo en el menú.
/// No es abstracta para que el diseñador de Visual Studio pueda abrir las clases hijas.
/// </summary>
public class ModuloBase : UserControl
{
    public ModuloBase()
    {
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;
        Dock = DockStyle.Fill;
        Padding = new Padding(24);
    }

    /// <summary>Acceso a Oracle. Toda consulta o acción pasa por aquí.</summary>
    protected static IBaseDatos Bd => Sesion.Bd;

    /// <summary>Módulo al que se atribuyen las acciones en la bitácora.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual Modulo Modulo => Modulo.General;

    /// <summary>
    /// Se llama cada vez que el usuario abre la pantalla desde el menú.
    /// Sobrescribir para cargar o refrescar los datos.
    /// </summary>
    public virtual Task AlActivarAsync() => Task.CompletedTask;

    /// <summary>
    /// Se llama al salir de la pantalla (otro módulo o cerrar la app).
    /// Sobrescribir para detener temporizadores (Performance).
    /// </summary>
    public virtual void AlDesactivar() { }
}

using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Seguridad de usuarios: pestañas Usuarios (#8, <see cref="UcUsuarios"/>), Roles y privilegios (#9, <see cref="UcRoles"/>)
/// y Perfiles (#10, P2). Cada pestaña es un UserControl aparte para que no haya conflictos entre quienes las programan.
/// </summary>
public partial class UcSeguridad : ModuloBase
{
    private readonly UcUsuarios _usuarios = new();
    private readonly UcRoles _roles = new();

    public UcSeguridad()
    {
        InitializeComponent();
        pestanas.Agregar("Usuarios", _usuarios);
        pestanas.Agregar("Roles y privilegios", _roles);
        // Issue #10 (P2): reemplazar esta línea por  pestanas.Agregar("Perfiles", new UcPerfiles());
        pestanas.Agregar("Perfiles", new Label { Text = "En construcción (issue #10).", ForeColor = Tema.TextoSecundario, Padding = new Padding(20) });
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
    }

    public override Modulo Modulo => Modulo.Seguridad;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    /// <summary>Carga solo la pestaña visible (cada una consulta lo suyo al abrirse).</summary>
    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _usuarios.CargarAsync(),
        1 => _roles.CargarAsync(),
        _ => Task.CompletedTask
    };
}

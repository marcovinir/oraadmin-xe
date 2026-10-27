using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Seguridad de usuarios: pestañas Usuarios (#8, <see cref="UcUsuarios"/>), Roles y privilegios (#9) y Perfiles (#10, P2).
/// Cada pestaña es un UserControl aparte para que no haya conflictos entre quienes las programan.
/// </summary>
public partial class UcSeguridad : ModuloBase
{
    public UcSeguridad()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Seguridad;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    private async void TabSeguridad_SelectedIndexChanged(object? sender, EventArgs e) => await CargarPestanaAsync();

    /// <summary>Carga solo la pestaña visible (cada una consulta lo suyo al abrirse).</summary>
    private Task CargarPestanaAsync() => tabSeguridad.SelectedTab == tabRoles ? ucRoles.CargarAsync() : ucUsuarios.CargarAsync();
}

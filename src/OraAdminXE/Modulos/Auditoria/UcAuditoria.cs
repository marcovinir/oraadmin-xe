using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>
/// Auditoría: políticas unificadas y punto de extensión para la pista de auditoría.
/// </summary>
public partial class UcAuditoria : ModuloBase
{
    private readonly UcPoliticasAuditoria _politicas = new();

    public UcAuditoria()
    {
        InitializeComponent();
        pestanas.Agregar("Políticas", _politicas);
        // Issue #20 agrega aquí UcPistaAuditoria sin cambiar la pestaña de políticas.
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
    }

    public override Modulo Modulo => Modulo.Auditoria;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    private Task CargarPestanaAsync() => pestanas.Seleccionada == 0
        ? _politicas.CargarAsync()
        : Task.CompletedTask;
}

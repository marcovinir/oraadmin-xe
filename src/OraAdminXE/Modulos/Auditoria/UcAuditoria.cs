using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>
/// Auditoría: políticas unificadas y punto de extensión para la pista de auditoría.
/// </summary>
public partial class UcAuditoria : ModuloBase
{
    private readonly UcPoliticasAuditoria _politicas = new();
    private readonly UcPistaAuditoria _pista = new();

    public UcAuditoria()
    {
        InitializeComponent();
        pestanas.Agregar("Políticas", _politicas);
        pestanas.Agregar("Pista de auditoría", _pista);
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
    }

    public override Modulo Modulo => Modulo.Auditoria;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _politicas.CargarAsync(),
        1 => _pista.CargarAsync(),
        _ => Task.CompletedTask
    };
}

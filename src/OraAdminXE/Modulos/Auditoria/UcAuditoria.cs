using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Auditoria;

/// <summary>
/// Auditoría: Políticas de auditoría unificada y pista de auditoría
/// PENDIENTE: ver las issues con la etiqueta del módulo en GitHub.
/// </summary>
public partial class UcAuditoria : ModuloBase
{
    public UcAuditoria()
    {
        InitializeComponent();
    }

    public override Modulo Modulo => Modulo.Auditoria;
}

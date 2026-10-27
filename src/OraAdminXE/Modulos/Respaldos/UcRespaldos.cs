using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Respaldos Full, por esquema y por tabla con DBMS_DATAPUMP: pestaña Nuevo respaldo (#11, <see cref="UcAsistenteRespaldo"/>).
/// </summary>
public partial class UcRespaldos : ModuloBase
{
    private readonly UcAsistenteRespaldo _asistente = new();

    public UcRespaldos()
    {
        InitializeComponent();
        pestanas.Agregar("Nuevo respaldo", _asistente);
    }

    public override Modulo Modulo => Modulo.Respaldos;

    public override Task AlActivarAsync() => _asistente.CargarAsync();
}

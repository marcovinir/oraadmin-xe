using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Respaldos Full, por esquema y por tabla con DBMS_DATAPUMP: pestañas Nuevo respaldo (#11, <see cref="UcAsistenteRespaldo"/>)
/// e Historial (#12, <see cref="UcCatalogoRespaldos"/>). Al terminar un respaldo se recarga el catálogo y se abre su log.
/// </summary>
public partial class UcRespaldos : ModuloBase
{
    private readonly UcAsistenteRespaldo _asistente = new();
    private readonly UcCatalogoRespaldos _catalogo = new();

    public UcRespaldos()
    {
        InitializeComponent();
        pestanas.Agregar("Nuevo respaldo", _asistente);
        pestanas.Agregar("Historial", _catalogo);
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
        _asistente.RespaldoIniciado += (_, _) => _catalogo.RespaldoEnCurso = true;
        _asistente.RespaldoTerminado += async (_, e) => await AlTerminarAsync(e.Solicitud, e.Resultado);
    }

    public override Modulo Modulo => Modulo.Respaldos;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _asistente.CargarAsync(),
        1 => _catalogo.CargarAsync(),
        _ => Task.CompletedTask
    };

    private async Task AlTerminarAsync(SolicitudRespaldo s, ResultadoRespaldo? resultado)
    {
        _catalogo.RespaldoEnCurso = false;
        await _catalogo.CargarAsync(resultado?.IdRespaldo);
        if (resultado is not null)
            DlgLog.Mostrar(this, Path.Combine(s.Directorio.RutaSo, s.ArchivoLog));
    }
}

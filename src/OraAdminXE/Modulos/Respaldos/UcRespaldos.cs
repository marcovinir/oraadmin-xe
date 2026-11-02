using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>
/// Respaldos Full, por esquema y por tabla con DBMS_DATAPUMP: pestañas Nuevo respaldo (#11, <see cref="UcAsistenteRespaldo"/>)
/// Historial (#12, <see cref="UcCatalogoRespaldos"/>) y Programados (#30, <see cref="UcProgramados"/>).
/// Al terminar un respaldo se recarga el catálogo y se abre su log.
/// </summary>
public partial class UcRespaldos : ModuloBase
{
    private readonly UcAsistenteRespaldo _asistente = new();
    private readonly UcCatalogoRespaldos _catalogo = new();
    private readonly UcProgramados _programados = new();

    public UcRespaldos()
    {
        InitializeComponent();
        pestanas.Agregar("Nuevo respaldo", _asistente);
        pestanas.Agregar("Historial", _catalogo);
        pestanas.Agregar("Programados", _programados);
        pestanas.SeleccionCambiada += async (_, _) => await CargarPestanaAsync();
        _asistente.RespaldoIniciado += (_, _) => _catalogo.RespaldoEnCurso = true;
        _asistente.RespaldoTerminado += async (_, e) => await AlTerminarAsync(e.Solicitud, e.Resultado);
        _programados.RespaldoLanzado += async (_, _) => await _catalogo.CargarAsync();
    }

    public override Modulo Modulo => Modulo.Respaldos;

    public override Task AlActivarAsync() => CargarPestanaAsync();

    private Task CargarPestanaAsync() => pestanas.Seleccionada switch
    {
        0 => _asistente.CargarAsync(),
        1 => _catalogo.CargarAsync(),
        2 => _programados.CargarAsync(),
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

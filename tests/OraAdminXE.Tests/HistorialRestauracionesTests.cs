using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Recuperacion;

namespace OraAdminXE.Tests;

/// <summary>Historial de restauraciones (issue #14).</summary>
public class HistorialRestauracionesTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task La_consulta_del_historial_trae_la_carpeta_del_log_y_los_objetos()
    {
        var historial = await _bd.ConsultarAsync(SqlRecuperacion.Historial);
        foreach (var columna in new[] { "ID_RESTAURACION", "FECHA_INICIO", "ARCHIVO_DUMP", "OBJETOS", "ESQUEMA_DESTINO", "ACCION_EXISTENTE",
                                        "DURACION_SEG", "ESTADO", "OBSERVACION", "RUTA_SO", "ARCHIVO_LOG" })
            Assert.True(historial.Columns.Contains(columna), columna);
    }

    [OracleFact]
    public async Task El_esquema_de_la_demo_existe_y_tiene_tablas_para_simular_el_desastre()
    {
        var tablas = await _bd.ConsultarAsync(SqlRecuperacion.TablasDe, Parametro.De("e", SqlRecuperacion.EsquemaDemo));
        Assert.Contains(tablas.AsEnumerable(), f => (string)f[0] == "DETALLE_FACTURA");
    }
}

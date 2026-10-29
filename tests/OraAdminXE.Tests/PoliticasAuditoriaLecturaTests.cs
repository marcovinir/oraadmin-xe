using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Auditoria;

namespace OraAdminXE.Tests;

/// <summary>Integraciones estrictamente de lectura para los catálogos de auditoría unificada.</summary>
public class PoliticasAuditoriaLecturaTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task Catalogos_de_politicas_se_consultan_sin_multiplicar_definiciones()
    {
        var tabla = await _bd.ConsultarAsync(SqlAuditoria.Politicas);
        Assert.Contains("POLICY_NAME", tabla.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));
        Assert.Contains("CANTIDAD_ACCIONES", tabla.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));
        Assert.All(tabla.AsEnumerable(), fila => Assert.True(Convert.ToInt32(fila["CANTIDAD_ACCIONES"]) > 0));
    }

    [OracleFact]
    public async Task Diagnostico_y_logon_predefinido_son_solo_lectura()
    {
        var diagnostico = await _bd.ConsultarAsync(SqlAuditoria.Diagnostico);
        Assert.Single(diagnostico.Rows);
        Assert.False(string.IsNullOrWhiteSpace(diagnostico.Rows[0]["CONTENEDOR"] as string));

        var logon = await _bd.ConsultarAsync(SqlAuditoria.OraLogonFailures);
        Assert.InRange(logon.Rows.Count, 0, 1);
    }
}

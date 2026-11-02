using OraAdminXE.Datos;
using OraAdminXE.Modulos.Auditoria;

namespace OraAdminXE.Tests;

/// <summary>Integración solo SELECT: no genera eventos, no limpia ni sincroniza la auditoría.</summary>
public class PistaAuditoriaLecturaTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task Pista_limitada_expone_columnas_requeridas()
    {
        var hasta = (DateTime)(await _bd.EscalarAsync(SqlPistaAuditoria.HoraBaseDatos))!;
        var consulta = SqlPistaAuditoria.CrearConsulta(new(hasta.AddDays(-1), hasta, null, null, null, false));
        var tabla = await _bd.ConsultarAsync(consulta.Sql, consulta.Parametros.ToArray());

        Assert.InRange(tabla.Rows.Count, 0, SqlPistaAuditoria.MaximoFilas);
        Assert.Contains("EVENT_TIMESTAMP", tabla.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));
        Assert.Contains("SQL_TEXT_PREVIEW", tabla.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));
        Assert.Contains("STATEMENT_ID", tabla.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));
    }

    [OracleFact]
    public async Task Listas_de_filtro_son_acotadas_y_solo_lectura()
    {
        var hasta = (DateTime)(await _bd.EscalarAsync(SqlPistaAuditoria.HoraBaseDatos))!;
        var usuarios = await _bd.ConsultarAsync(SqlPistaAuditoria.UsuariosRecientes,
            Parametro.De("desde", hasta.AddDays(-1)), Parametro.De("hasta", hasta));
        var politicas = await _bd.ConsultarAsync(SqlPistaAuditoria.PoliticasDisponibles);

        Assert.InRange(usuarios.Rows.Count, 0, SqlPistaAuditoria.MaximoUsuarios);
        Assert.InRange(politicas.Rows.Count, 0, SqlPistaAuditoria.MaximoPoliticas);
    }
}

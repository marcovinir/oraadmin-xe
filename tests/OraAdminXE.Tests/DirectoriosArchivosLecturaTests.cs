using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Directorios;

namespace OraAdminXE.Tests;

/// <summary>Contratos de #18 contra Oracle; consultas solamente, sin DML ni UTL_FILE.</summary>
public class DirectoriosArchivosLecturaTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task Catalogo_de_archivos_expone_directorios_activos_y_campos_de_asociacion()
    {
        var directorios = await _bd.ConsultarAsync(SqlDirectorios.DirectoriosActivosArchivos);
        Assert.All(directorios.AsEnumerable(), fila =>
        {
            Assert.False(string.IsNullOrWhiteSpace((string)fila["NOMBRE_ORACLE"]));
            Assert.False(string.IsNullOrWhiteSpace((string)fila["RUTA_CATALOGO"]));
            Assert.False(string.IsNullOrWhiteSpace((string)fila["DIRECTORY_PATH"]));
        });

        var columnas = await _bd.ConsultarAsync("""
            SELECT column_name FROM user_tab_columns
            WHERE table_name = 'RESPALDO' AND column_name IN ('ID_RESPALDO', 'ID_DIRECTORIO', 'ARCHIVO_DUMP', 'ARCHIVO_LOG', 'ESTADO')
            """);
        Assert.Equal(5, columnas.Rows.Count);
    }

    [OracleFact]
    public async Task Grants_y_vistas_necesarios_para_guardas_se_pueden_consultar_sin_escribir()
    {
        var grants = await _bd.ConsultarAsync(SqlDirectorios.PrivilegiosArchivosSesion, Parametro.De("directorio", "DIR_RESPALDOS"));
        var nombres = grants.AsEnumerable().Select(f => (string)f["PRIVILEGIO"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("DIRECTORY_READ", nombres);
        Assert.Contains("DIRECTORY_WRITE", nombres);
        Assert.Contains("UTL_FILE", nombres);

        var jobs = await _bd.ConsultarAsync("SELECT COUNT(*) AS total FROM dba_scheduler_running_jobs");
        Assert.Single(jobs.Rows);
    }
}

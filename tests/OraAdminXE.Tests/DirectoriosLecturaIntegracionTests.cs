using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Directorios;

namespace OraAdminXE.Tests;

/// <summary>
/// Verificaciones de solo lectura contra la instalación local. No contiene DDL, DML, grants ni limpieza.
/// Se salta automáticamente sin ORAADMIN_TEST_CLAVE.
/// </summary>
public class DirectoriosLecturaIntegracionTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task Dir_respaldos_existe_esta_registrado_activo_y_su_carpeta_local_existe()
    {
        var tabla = await _bd.ConsultarAsync(SqlDirectorios.Directorios);
        var fila = tabla.Select("DIRECTORY_NAME = 'DIR_RESPALDOS'").Single();

        Assert.Equal("SI", fila["REGISTRADO"]);
        Assert.Equal("SI", fila["ACTIVO"]);
        Assert.True(Directory.Exists((string)fila["DIRECTORY_PATH"]), "DIR_RESPALDOS debe apuntar a una carpeta local existente en la instalación de prueba.");
    }

    [OracleFact]
    public async Task Directorios_visibles_incluyen_nombre_y_ruta()
    {
        var tabla = await _bd.ConsultarAsync(SqlDirectorios.Directorios);

        Assert.NotEmpty(tabla.Rows);
        Assert.All(tabla.AsEnumerable(), fila =>
        {
            Assert.False(string.IsNullOrWhiteSpace((string)fila["DIRECTORY_NAME"]));
            Assert.False(string.IsNullOrWhiteSpace((string)fila["DIRECTORY_PATH"]));
        });
    }

    [OracleFact]
    public async Task Privilegios_de_dir_respaldos_se_pueden_consultar_sin_escribir()
    {
        var tabla = await _bd.ConsultarAsync(SqlDirectorios.PrivilegiosDe, Parametro.De("directorio", "DIR_RESPALDOS"));
        var privilegios = tabla.AsEnumerable().Select(fila => (string)fila["PRIVILEGE"]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("READ", privilegios);
        Assert.Contains("WRITE", privilegios);
    }
}

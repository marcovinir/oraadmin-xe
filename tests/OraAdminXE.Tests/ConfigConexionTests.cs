using OraAdminXE.Datos;

namespace OraAdminXE.Tests;

public class ConfigConexionTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"conexion_{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_ruta);

    [Fact]
    public void Cargar_sin_archivo_devuelve_valores_por_defecto()
    {
        var config = ConfigConexion.Cargar(_ruta);
        Assert.Equal("localhost:1521/XEPDB1", config.DataSource);
        Assert.Equal("ADMIN_BD", config.Usuario);
    }

    [Fact]
    public void Guardar_y_cargar_conserva_los_datos()
    {
        new ConfigConexion { Host = "servidor", Puerto = 1522, Servicio = "PDB2", Usuario = "DBA2" }.Guardar(_ruta);

        var config = ConfigConexion.Cargar(_ruta);

        Assert.Equal("servidor:1522/PDB2", config.DataSource);
        Assert.Equal("DBA2", config.Usuario);
    }

    [Fact]
    public void Guardar_nunca_escribe_una_contrasena()
    {
        new ConfigConexion().Guardar(_ruta);
        var json = File.ReadAllText(_ruta);
        Assert.DoesNotContain("clave", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cargar_un_archivo_danado_devuelve_valores_por_defecto()
    {
        File.WriteAllText(_ruta, "{ esto no es json");
        Assert.Equal("localhost:1521/XEPDB1", ConfigConexion.Cargar(_ruta).DataSource);
    }
}

using System.Data;
using OraAdminXE.Modulos.Performance;

namespace OraAdminXE.Tests;

public class ServicioSesionesTests
{
    [Fact]
    public void MatarSesion_genera_ddl_valido()
    {
        var sql = ServicioSesiones.MatarSesion(12, 345, esLaApp: false);
        Assert.Equal("ALTER SYSTEM KILL SESSION '12,345' IMMEDIATE", sql);
    }

    [Fact]
    public void MatarSesion_impide_matar_la_propia_app()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ServicioSesiones.MatarSesion(12, 345, esLaApp: true));
        Assert.Contains("propia aplicación", ex.Message);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(10, -5)]
    public void MatarSesion_rechaza_identificadores_no_positivos(int sid, int serial)
    {
        Assert.Throws<ArgumentException>(() =>
            ServicioSesiones.MatarSesion(sid, serial, esLaApp: false));
    }

    [Fact]
    public void MatarSesion_acepta_serial_cero()
    {
        Assert.Equal("ALTER SYSTEM KILL SESSION '10,0' IMMEDIATE",
            ServicioSesiones.MatarSesion(10, 0, esLaApp: false));
    }

    [Fact]
    public void ObtenerSidsBloqueadores_extrae_sids_unicos_de_bloqueo()
    {
        var dt = new DataTable();
        dt.Columns.Add("SID", typeof(int));
        dt.Columns.Add("BLOCKING_SESSION", typeof(object));

        dt.Rows.Add(10, DBNull.Value);
        dt.Rows.Add(20, 10);
        dt.Rows.Add(30, 10);
        dt.Rows.Add(40, 50);

        var sids = ServicioSesiones.ObtenerSidsBloqueadores(dt);

        Assert.Equal(2, sids.Count);
        Assert.Contains(10, sids);
        Assert.Contains(50, sids);
        Assert.DoesNotContain(20, sids);
    }

    [Fact]
    public void FormatearBloqueos_genera_descripcion_con_objeto_bloqueado()
    {
        var dtSesiones = new DataTable();
        dtSesiones.Columns.Add("SID", typeof(int));
        dtSesiones.Columns.Add("USERNAME", typeof(string));
        dtSesiones.Columns.Add("BLOCKING_SESSION", typeof(object));

        dtSesiones.Rows.Add(15, "JUAN", DBNull.Value);
        dtSesiones.Rows.Add(28, "DEMO_VENTAS", 15);

        var dtBloqueos = new DataTable();
        dtBloqueos.Columns.Add("SID", typeof(int));
        dtBloqueos.Columns.Add("OWNER", typeof(string));
        dtBloqueos.Columns.Add("OBJECT_NAME", typeof(string));
        dtBloqueos.Columns.Add("MODO", typeof(string));

        dtBloqueos.Rows.Add(15, "DEMO_VENTAS", "CLIENTE", "ROW EXCLUSIVE");

        var resultado = ServicioSesiones.FormatearBloqueos(dtSesiones, dtBloqueos);

        Assert.Single(resultado);
        Assert.Contains("SID 15 (JUAN) bloquea a SID 28 (DEMO_VENTAS)", resultado[0]);
        Assert.Contains("DEMO_VENTAS.CLIENTE (ROW EXCLUSIVE)", resultado[0]);
    }

    [Fact]
    public void FormatearBloqueos_lista_vacia_si_no_hay_bloqueos()
    {
        var dtSesiones = new DataTable();
        dtSesiones.Columns.Add("SID", typeof(int));
        dtSesiones.Columns.Add("USERNAME", typeof(string));
        dtSesiones.Columns.Add("BLOCKING_SESSION", typeof(object));

        dtSesiones.Rows.Add(1, "ADMIN_BD", DBNull.Value);
        dtSesiones.Rows.Add(2, "JUAN", DBNull.Value);

        var dtBloqueos = new DataTable();
        dtBloqueos.Columns.Add("SID", typeof(int));
        dtBloqueos.Columns.Add("OWNER", typeof(string));
        dtBloqueos.Columns.Add("OBJECT_NAME", typeof(string));
        dtBloqueos.Columns.Add("MODO", typeof(string));

        var resultado = ServicioSesiones.FormatearBloqueos(dtSesiones, dtBloqueos);

        Assert.Empty(resultado);
    }
}

using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Tests;

/// <summary>
/// Respaldos reales con DBMS_DATAPUMP contra Oracle (issue #11). Al terminar borra los archivos,
/// las filas del catálogo y el directorio de prueba, para no ensuciar el catálogo de la demo.
/// </summary>
[Collection(ColeccionDataPump.Nombre)]
public class RespaldosIntegracionTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _sufijo = DateTime.Now.ToString("HHmmssfff");
    private readonly List<string> _jobs = [];
    private DirectorioRespaldo _dir = null!;
    private string? _dirMalo;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave))) return;
        await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
        var fila = (await _bd.ConsultarAsync(SqlRespaldos.Directorios)).Select("NOMBRE_ORACLE = 'DIR_RESPALDOS'").Single();
        _dir = new(Convert.ToInt32(fila["ID_DIRECTORIO"]), "DIR_RESPALDOS", (string)fila["RUTA_SO"]);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        foreach (var job in _jobs)
        {
            foreach (var ext in new[] { ".DMP", ".LOG" })
                File.Delete(Path.Combine(_dir.RutaSo, job + ext));
            await LimpiezaOracle.SinBitacoraAsync(_bd, "DELETE FROM admin_bd.respaldo WHERE nombre_job = :j", Parametro.De("j", job));
        }
        if (_dirMalo is not null)
        {
            await LimpiezaOracle.SinBitacoraAsync(_bd, "DELETE FROM admin_bd.directorio WHERE nombre_oracle = :d", Parametro.De("d", _dirMalo));
            await LimpiezaOracle.SinBitacoraAsync(_bd, $"DROP DIRECTORY {_dirMalo}");
        }
        await LimpiezaOracle.BorrarBitacoraAsync(_bd, _sufijo);
    }

    private string Job(string tipo)
    {
        var job = $"PRUEBA_EXP_{tipo}_{_sufijo}";
        _jobs.Add(job);
        return job;
    }

    private async Task<DataRow> Catalogo(string job) =>
        (await _bd.ConsultarAsync("SELECT * FROM admin_bd.v_respaldos WHERE nombre_job = :j", Parametro.De("j", job))).Rows[0];

    [OracleFact]
    public async Task Respaldo_por_tabla_queda_completado_con_su_archivo_catalogo_y_bitacora()
    {
        var servicio = new ServicioRespaldos(_bd);
        var s = new SolicitudRespaldo(TipoRespaldo.Table, _dir, Job("TABLE"), ["DEMO_VENTAS"], ["PRODUCTO", "CLIENTE"], "Prueba automática");
        await servicio.ValidarDisponibleAsync(s);

        var r = await servicio.EjecutarAsync(s);

        Assert.True(r.Completado, r.EstadoJob);
        Assert.True(File.Exists(Path.Combine(_dir.RutaSo, s.ArchivoDump)));
        Assert.Equal(new FileInfo(Path.Combine(_dir.RutaSo, s.ArchivoDump)).Length, r.TamanoBytes);
        var fila = await Catalogo(s.Job);
        Assert.Equal("COMPLETADO", fila["ESTADO"]);
        Assert.Equal("TABLE", fila["TIPO"]);
        Assert.False(fila["DURACION_SEG"] is DBNull);
        var objetos = await _bd.ConsultarAsync(
            "SELECT esquema || '.' || tabla FROM admin_bd.respaldo_objeto WHERE id_respaldo = :id ORDER BY 1", Parametro.De("id", r.IdRespaldo));
        Assert.Equal(["DEMO_VENTAS.CLIENTE", "DEMO_VENTAS.PRODUCTO"], objetos.AsEnumerable().Select(f => (string)f[0]));
        var bitacora = await _bd.EscalarAsync(
            "SELECT resultado FROM admin_bd.v_bitacora WHERE modulo = 'RESPALDOS' AND mensaje LIKE :m ORDER BY fecha DESC FETCH FIRST 1 ROW ONLY",
            Parametro.De("m", s.ArchivoDump + "%"));
        Assert.Equal("OK", bitacora);

        // Con el mismo nombre ya no se puede repetir
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ValidarDisponibleAsync(s));
    }

    [OracleFact]
    public async Task Respaldo_por_esquema_queda_completado()
    {
        var s = new SolicitudRespaldo(TipoRespaldo.Schema, _dir, Job("SCHEMA"), ["DEMO_VENTAS"], []);
        var r = await new ServicioRespaldos(_bd).EjecutarAsync(s);
        Assert.True(r.Completado, r.EstadoJob);
        Assert.InRange(r.TamanoMb, 3, 20);
    }

    [OracleFact]
    public async Task Directorio_con_ruta_inexistente_queda_en_error_con_ORA_39070()
    {
        _dirMalo = "DIR_PRUEBA_" + _sufijo;
        await _bd.EjecutarAsync(Modulo.General, "Prueba", $@"CREATE OR REPLACE DIRECTORY {_dirMalo} AS 'C:\no_existe_{_sufijo}'");
        await LimpiezaOracle.SinBitacoraAsync(_bd,
            "INSERT INTO admin_bd.directorio (nombre_oracle, ruta_so, activo) VALUES (:d, :r, 'N')",
            Parametro.De("d", _dirMalo), Parametro.De("r", $@"C:\no_existe_{_sufijo}"));
        var id = Convert.ToInt32(await _bd.EscalarAsync("SELECT id_directorio FROM admin_bd.directorio WHERE nombre_oracle = :d", Parametro.De("d", _dirMalo)));
        var malo = new DirectorioRespaldo(id, _dirMalo, $@"C:\no_existe_{_sufijo}");
        var s = new SolicitudRespaldo(TipoRespaldo.Table, malo, Job("ERR"), ["DEMO_VENTAS"], ["PRODUCTO"]);

        var ex = await Assert.ThrowsAsync<OracleException>(() => new ServicioRespaldos(_bd).EjecutarAsync(s));

        Assert.Contains("ORA-39070", ex.Message);
        var fila = await Catalogo(s.Job);
        Assert.Equal("ERROR", fila["ESTADO"]);
        Assert.Contains("ORA-39070", (string)fila["OBSERVACION"]);
        Assert.Null(await _bd.EscalarAsync("SELECT state FROM dba_datapump_jobs WHERE job_name = :j", Parametro.De("j", s.Job)));
    }
}

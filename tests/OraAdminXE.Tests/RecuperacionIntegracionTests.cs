using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Recuperacion;
using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Tests;

/// <summary>
/// Respaldo → desastre → restauración real con DBMS_DATAPUMP (issue #13). Usa un esquema propio de la prueba
/// (no toca DEMO_VENTAS) y al terminar borra los usuarios, los archivos y las filas del catálogo.
/// </summary>
[Collection(ColeccionDataPump.Nombre)]
public class RecuperacionIntegracionTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _sufijo = DateTime.Now.ToString("HHmmssfff");
    private string Origen => "PRUEBA_REC_" + _sufijo;
    private string Copia => "PRUEBA_COPIA_" + _sufijo;
    private DirectorioRespaldo _dir = null!;
    private readonly List<string> _archivos = [];

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave))) return;
        await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
        var fila = (await _bd.ConsultarAsync(SqlRespaldos.Directorios)).Select("NOMBRE_ORACLE = 'DIR_RESPALDOS'").Single();
        _dir = new(Convert.ToInt32(fila["ID_DIRECTORIO"]), "DIR_RESPALDOS", (string)fila["RUTA_SO"]);
        await Sql($"CREATE USER {Origen} IDENTIFIED BY \"Prueba_2026\" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS");
        await Sql($"CREATE TABLE {Origen}.VENTA (ID NUMBER PRIMARY KEY, MONTO NUMBER)");
        await Sql($"INSERT INTO {Origen}.VENTA SELECT LEVEL, LEVEL * 10 FROM dual CONNECT BY LEVEL <= 5000");
        await Sql($"CREATE TABLE {Origen}.CLIENTE (ID NUMBER PRIMARY KEY)");
        await Sql($"INSERT INTO {Origen}.CLIENTE SELECT LEVEL FROM dual CONNECT BY LEVEL <= 10");
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        foreach (var u in new[] { Origen, Copia })
            await LimpiezaOracle.SinBitacoraAsync(_bd, $"DROP USER {u} CASCADE");
        await LimpiezaOracle.SinBitacoraAsync(_bd,
            "DELETE FROM admin_bd.restauracion WHERE id_respaldo IN (SELECT id_respaldo FROM admin_bd.respaldo WHERE nombre_job LIKE :j)", Parametro.De("j", $"PRUEBA_%{_sufijo}"));
        await LimpiezaOracle.SinBitacoraAsync(_bd, "DELETE FROM admin_bd.respaldo WHERE nombre_job LIKE :j", Parametro.De("j", $"PRUEBA_%{_sufijo}"));
        foreach (var archivo in _archivos) File.Delete(Path.Combine(_dir.RutaSo, archivo));
        await LimpiezaOracle.BorrarBitacoraAsync(_bd, _sufijo);
    }

    private Task<int> Sql(string sql) => _bd.EjecutarAsync(Modulo.General, "Prueba", sql);

    private async Task<long> Filas(string tabla) => Convert.ToInt64(await _bd.EscalarAsync($"SELECT COUNT(*) FROM {tabla}"));

    private async Task<RespaldoRestaurable> RespaldarAsync()
    {
        var s = new SolicitudRespaldo(TipoRespaldo.Schema, _dir, $"PRUEBA_EXP_{_sufijo}", [Origen], []);
        _archivos.AddRange([s.ArchivoDump, s.ArchivoLog]);
        var r = await new ServicioRespaldos(_bd).EjecutarAsync(s);
        Assert.True(r.Completado, r.EstadoJob);
        return new(r.IdRespaldo, TipoRespaldo.Schema, _dir.NombreOracle, _dir.RutaSo, s.ArchivoDump, s.ArchivoLog, [(Origen, null)]);
    }

    private async Task<ResultadoRestauracion> RestaurarAsync(SolicitudRestauracion s)
    {
        _archivos.Add(s.ArchivoLog);
        return await new ServicioRecuperacion(_bd).EjecutarAsync(s);
    }

    [OracleFact]
    public async Task Borrar_una_tabla_y_restaurarla_desde_el_respaldo_del_esquema()
    {
        var respaldo = await RespaldarAsync();
        await Sql($"DROP TABLE {Origen}.VENTA PURGE");

        var s = new SolicitudRestauracion(respaldo, AlcanceRestauracion.Tablas, [Origen], ["VENTA"], AccionExistente.Replace, null, DateTime.Now);
        var r = await RestaurarAsync(s);

        Assert.True(r.Completado, r.EstadoJob);
        Assert.Equal(0, r.ErroresLog);
        Assert.Equal(5000, await Filas($"{Origen}.VENTA"));
        Assert.Equal([($"{Origen}.VENTA", (long?)5000)], r.Conteos);

        var fila = (await _bd.ConsultarAsync("SELECT * FROM admin_bd.v_restauraciones WHERE id_restauracion = :id", Parametro.De("id", r.IdRestauracion))).Rows[0];
        Assert.Equal("COMPLETADO", fila["ESTADO"]);
        Assert.Equal("REPLACE", fila["ACCION_EXISTENTE"]);
        Assert.Equal(1, Convert.ToInt32(await _bd.EscalarAsync(
            "SELECT COUNT(*) FROM admin_bd.restauracion_objeto WHERE id_restauracion = :id AND tabla = 'VENTA'", Parametro.De("id", r.IdRestauracion))));
        Assert.Equal("OK", await _bd.EscalarAsync(
            "SELECT resultado FROM admin_bd.v_bitacora WHERE modulo = 'RECUPERACION' AND mensaje LIKE :m ORDER BY fecha DESC FETCH FIRST 1 ROW ONLY",
            Parametro.De("m", $"%{s.ArchivoLog}%")));
    }

    [OracleFact]
    public async Task Restaurar_el_esquema_en_otro_esquema_crea_el_usuario_con_sus_tablas()
    {
        var respaldo = await RespaldarAsync();

        var s = new SolicitudRestauracion(respaldo, AlcanceRestauracion.Esquemas, [Origen], [], AccionExistente.Skip, Copia, DateTime.Now);
        var r = await RestaurarAsync(s);

        Assert.True(r.Completado, r.EstadoJob);
        Assert.NotNull(await _bd.EscalarAsync("SELECT username FROM dba_users WHERE username = :u", Parametro.De("u", Copia)));
        Assert.Equal(5000, await Filas($"{Copia}.VENTA"));
        Assert.Equal(10, await Filas($"{Copia}.CLIENTE"));
        Assert.Contains(($"{Copia}.VENTA", (long?)5000), r.Conteos);
    }

    [OracleFact]
    public async Task Skip_sobre_una_tabla_que_existe_la_omite_y_lo_avisa()
    {
        var respaldo = await RespaldarAsync();
        await Sql($"DELETE FROM {Origen}.VENTA WHERE id > 100");
        var s = new SolicitudRestauracion(respaldo, AlcanceRestauracion.Tablas, [Origen], ["VENTA"], AccionExistente.Skip, null, DateTime.Now);

        var r = await RestaurarAsync(s);

        Assert.True(r.Completado, r.EstadoJob);
        Assert.Equal(1, r.TablasOmitidas);
        Assert.Equal([($"{Origen}.VENTA", (long?)100)], r.Conteos);   // no se tocó
    }

    [OracleFact]
    public async Task Esquema_sobre_si_mismo_termina_con_errores_ORA_en_el_log()
    {
        var respaldo = await RespaldarAsync();
        var s = new SolicitudRestauracion(respaldo, AlcanceRestauracion.Esquemas, [Origen], [], AccionExistente.Skip, null, DateTime.Now);

        var r = await RestaurarAsync(s);

        Assert.True(r.Completado, r.EstadoJob);
        Assert.True(r.ErroresLog > 0);   // ORA-31684: el usuario ya existe
        var obs = await _bd.EscalarAsync("SELECT observacion FROM admin_bd.restauracion WHERE id_restauracion = :id", Parametro.De("id", r.IdRestauracion));
        Assert.Contains("ORA-", (string)obs!);
    }
}

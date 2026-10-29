using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Tests;

/// <summary>Bloques de los respaldos programados (issue #30), sin base de datos.</summary>
public class ProgramacionesTests
{
    private static readonly DirectorioRespaldo Dir = new(1, "DIR_RESPALDOS", @"C:\respaldos_bd");

    [Theory]
    [InlineData(TipoFrecuencia.CadaHoras, "FREQ=HOURLY;INTERVAL=6;BYMINUTE=0;BYSECOND=0")]
    [InlineData(TipoFrecuencia.Diaria, "FREQ=DAILY;BYHOUR=22;BYMINUTE=30;BYSECOND=0")]
    [InlineData(TipoFrecuencia.Semanal, "FREQ=WEEKLY;BYDAY=MON;BYHOUR=22;BYMINUTE=30;BYSECOND=0")]
    public void Frecuencia_a_repeat_interval(TipoFrecuencia tipo, string esperado) =>
        Assert.Equal(esperado, new Frecuencia(tipo, Horas: 6, Hora: 22, Minuto: 30, Dia: DayOfWeek.Monday).RepeatInterval);

    [Fact]
    public void Bloque_del_job_registra_el_respaldo_y_la_bitacora()
    {
        var bloque = ServicioProgramaciones.BloqueJob(new(TipoRespaldo.Schema, Dir, "prg_demo", ["DEMO_VENTAS"], []));
        Assert.Contains("n VARCHAR2(128) := 'PRG_DEMO_' || TO_CHAR(SYSDATE, 'YYYYMMDD_HH24MISS');", bloque);
        Assert.Contains("INSERT INTO admin_bd.respaldo_objeto (id_respaldo, esquema, tabla) VALUES (id, 'DEMO_VENTAS', NULL);", bloque);
        Assert.Contains("DBMS_DATAPUMP.OPEN('EXPORT', 'SCHEMA', NULL, n);", bloque);
        Assert.Contains("'SCHEMA_EXPR', 'IN (''DEMO_VENTAS'')'", bloque);
        Assert.Contains("'Respaldo programado PRG_DEMO'", bloque);
        Assert.Contains("RAISE;", bloque);
        Assert.InRange(bloque.Length, 1, ServicioProgramaciones.MaxAccion);
    }

    [Fact]
    public void Bloque_crear_lleva_la_programacion_y_el_job()
    {
        var bloque = ServicioProgramaciones.BloqueCrear(new(TipoRespaldo.Table, Dir, "PRG_FACTURAS", ["DEMO_VENTAS"], ["FACTURA"]),
            new Frecuencia(TipoFrecuencia.Diaria, Hora: 22, Minuto: 0));
        Assert.Contains("'PRG_FACTURAS', 'FREQ=DAILY;BYHOUR=22;BYMINUTE=0;BYSECOND=0', 'S')", bloque);
        Assert.Contains("VALUES (id, 'DEMO_VENTAS', 'FACTURA');", bloque);
        Assert.Contains("job_action      => q'[DECLARE", bloque);
        Assert.Contains("repeat_interval => 'FREQ=DAILY;BYHOUR=22;BYMINUTE=0;BYSECOND=0'", bloque);
    }

    [Fact]
    public void Acciones_sobre_el_job()
    {
        Assert.Contains("DBMS_SCHEDULER.ENABLE('PRG_X');", ServicioProgramaciones.BloqueActivar("prg_x", true));
        Assert.Contains("SET activo = 'N'", ServicioProgramaciones.BloqueActivar("PRG_X", false));
        Assert.Contains("RUN_JOB('PRG_X', use_current_session => FALSE)", ServicioProgramaciones.BloqueEjecutarAhora("PRG_X"));
        Assert.Contains("DROP_JOB('PRG_X', force => TRUE)", ServicioProgramaciones.BloqueEliminar("PRG_X", existeJob: true));
        Assert.DoesNotContain("DROP_JOB", ServicioProgramaciones.BloqueEliminar("PRG_X", existeJob: false));
    }

    [Fact]
    public void Validaciones()
    {
        Assert.Throws<ArgumentException>(() => ServicioProgramaciones.BloqueJob(new(TipoRespaldo.Schema, Dir, new string('P', 101), ["DEMO_VENTAS"], [])));
        Assert.Throws<ArgumentException>(() => ServicioProgramaciones.BloqueJob(new(TipoRespaldo.Schema, Dir, "PRG_X", [], [])));
        Assert.Throws<ArgumentException>(() => ServicioProgramaciones.BloqueActivar("X'); DROP", true));
    }
}

/// <summary>Ciclo completo contra Oracle: crear → ejecutar ya → el respaldo aparece en el catálogo → desactivar → eliminar.</summary>
[Collection(ColeccionDataPump.Nombre)]
public class ProgramacionesIntegracionTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _sufijo = DateTime.Now.ToString("HHmmssfff");
    private string Job => "PRG_PRUEBA_" + _sufijo;
    private DirectorioRespaldo _dir = null!;

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
        await LimpiezaOracle.SinBitacoraAsync(_bd, $"BEGIN DBMS_SCHEDULER.DROP_JOB('{Job}', force => TRUE); END;");
        await LimpiezaOracle.SinBitacoraAsync(_bd, "DELETE FROM admin_bd.programacion WHERE nombre_job = :j", Parametro.De("j", Job));
        var archivos = await _bd.ConsultarAsync("SELECT archivo_dump, archivo_log FROM admin_bd.respaldo WHERE nombre_job LIKE :j", Parametro.De("j", Job + "%"));
        foreach (DataRow r in archivos.Rows)
            foreach (var a in new[] { (string)r[0], (string)r[1] }) File.Delete(Path.Combine(_dir.RutaSo, a));
        await LimpiezaOracle.SinBitacoraAsync(_bd, "DELETE FROM admin_bd.respaldo WHERE nombre_job LIKE :j", Parametro.De("j", Job + "%"));
        await LimpiezaOracle.BorrarBitacoraAsync(_bd, Job);
    }

    private Task<object?> Estado() => _bd.EscalarAsync("SELECT state FROM dba_scheduler_jobs WHERE owner = USER AND job_name = :j", Parametro.De("j", Job));

    [OracleFact]
    public async Task Crear_ejecutar_ya_desactivar_y_eliminar()
    {
        var s = new SolicitudRespaldo(TipoRespaldo.Table, _dir, Job, ["DEMO_VENTAS"], ["PRODUCTO"]);
        await _bd.EjecutarAsync(Modulo.Respaldos, "Programar respaldo", ServicioProgramaciones.BloqueCrear(s, new Frecuencia(TipoFrecuencia.CadaHoras, Horas: 1)));

        Assert.Equal("SCHEDULED", await Estado());
        var lista = (await _bd.ConsultarAsync(SqlProgramaciones.Lista)).Select($"NOMBRE_JOB = '{Job}'").Single();
        Assert.Equal("DEMO_VENTAS.PRODUCTO", lista["OBJETOS"]);
        Assert.True((DateTime)lista["PROXIMA"] > DateTime.Now, "El primer respaldo no debe correr al crear la programación");

        await _bd.EjecutarAsync(Modulo.Respaldos, "Ejecutar ahora", ServicioProgramaciones.BloqueEjecutarAhora(Job));
        string? estado = null;
        for (var i = 0; i < 60 && estado is null or "EN_PROCESO"; i++)
        {
            await Task.Delay(2000);
            estado = (string?)await _bd.EscalarAsync(
                "SELECT estado FROM admin_bd.respaldo WHERE nombre_job LIKE :j ORDER BY id_respaldo DESC FETCH FIRST 1 ROW ONLY", Parametro.De("j", Job + "_%"));
        }
        Assert.Equal("COMPLETADO", estado);
        Assert.Equal("OK", await _bd.EscalarAsync(
            "SELECT resultado FROM admin_bd.v_bitacora WHERE accion = :a ORDER BY fecha DESC FETCH FIRST 1 ROW ONLY", Parametro.De("a", $"Respaldo programado {Job}")));

        await _bd.EjecutarAsync(Modulo.Respaldos, "Desactivar", ServicioProgramaciones.BloqueActivar(Job, false));
        Assert.Equal("DISABLED", await Estado());
        Assert.Equal("N", await _bd.EscalarAsync("SELECT activo FROM admin_bd.programacion WHERE nombre_job = :j", Parametro.De("j", Job)));

        await _bd.EjecutarAsync(Modulo.Respaldos, "Eliminar", ServicioProgramaciones.BloqueEliminar(Job, existeJob: true));
        Assert.Null(await Estado());
        Assert.Null(await _bd.EscalarAsync("SELECT nombre_job FROM admin_bd.programacion WHERE nombre_job = :j", Parametro.De("j", Job)));
    }
}

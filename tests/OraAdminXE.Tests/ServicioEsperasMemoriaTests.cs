using System.Data;
using OraAdminXE.Modulos.Performance;
using Xunit;

namespace OraAdminXE.Tests;

public class ServicioEsperasMemoriaTests
{
    [Theory]
    [InlineData("cpu_time", "cpu_time")]
    [InlineData("buffer_gets", "buffer_gets")]
    [InlineData("disk_reads", "disk_reads")]
    [InlineData("elapsed_time", "elapsed_time")]
    [InlineData("otro", "elapsed_time")]
    [InlineData(null, "elapsed_time")]
    [InlineData("; DROP TABLE dual; --", "elapsed_time")]
    public void ValidarCriterioTopSql_PrevieneInyeccionYValidaColumnas(string? entrada, string esperado)
    {
        var resultado = ServicioEsperasMemoria.ValidarCriterioTopSql(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void SqlTopSql_UsaColumnaEsperadaYParametrosSeguros()
    {
        var sqlElapsed = SqlPerformance.SqlTopSql("elapsed_time");
        Assert.Contains("ORDER BY elapsed_time DESC", sqlElapsed);
        Assert.Contains("FETCH FIRST 10 ROWS ONLY", sqlElapsed);

        var sqlCpu = SqlPerformance.SqlTopSql("cpu_time");
        Assert.Contains("ORDER BY cpu_time DESC", sqlCpu);

        var sqlInvalido = SqlPerformance.SqlTopSql("malicioso");
        Assert.Contains("ORDER BY elapsed_time DESC", sqlInvalido);
    }

    [Fact]
    public void ParsearEventos_MapeaCorrectamente()
    {
        using var dt = new DataTable();
        dt.Columns.Add("EVENT", typeof(string));
        dt.Columns.Add("WAIT_CLASS", typeof(string));
        dt.Columns.Add("TOTAL_WAITS", typeof(long));
        dt.Columns.Add("SEG", typeof(double));

        dt.Rows.Add("db file sequential read", "User I/O", 15200L, 45.8);

        var lista = ServicioEsperasMemoria.ParsearEventos(dt);

        Assert.Single(lista);
        Assert.Equal("db file sequential read", lista[0].Evento);
        Assert.Equal("User I/O", lista[0].Clase);
        Assert.Equal(15200L, lista[0].TotalWaits);
        Assert.Equal(45.8, lista[0].Segundos);
    }

    [Fact]
    public void ParsearTopSql_MapeaCamposCompletos()
    {
        using var dt = new DataTable();
        dt.Columns.Add("SQL_ID", typeof(string));
        dt.Columns.Add("EXECUTIONS", typeof(long));
        dt.Columns.Add("SEG", typeof(double));
        dt.Columns.Add("CPU_SEG", typeof(double));
        dt.Columns.Add("BUFFER_GETS", typeof(long));
        dt.Columns.Add("DISK_READS", typeof(long));
        dt.Columns.Add("TEXTO", typeof(string));

        dt.Rows.Add("g48kd9a123", 500L, 12.34, 10.20, 98000L, 120L, "SELECT * FROM demo_ventas.cliente");

        var lista = ServicioEsperasMemoria.ParsearTopSql(dt);

        Assert.Single(lista);
        Assert.Equal("g48kd9a123", lista[0].SqlId);
        Assert.Equal(500L, lista[0].Ejecuciones);
        Assert.Equal(12.34, lista[0].Segundos);
        Assert.Equal(10.20, lista[0].CpuSegundos);
        Assert.Equal(98000L, lista[0].BufferGets);
        Assert.Equal(120L, lista[0].DiskReads);
        Assert.Equal("SELECT * FROM demo_ventas.cliente", lista[0].Texto);
    }

    [Fact]
    public void ParsearSgaYPga_MapeanValoresEnMb()
    {
        using var dtSga = new DataTable();
        dtSga.Columns.Add("NAME", typeof(string));
        dtSga.Columns.Add("MB", typeof(long));
        dtSga.Columns.Add("RESIZEABLE", typeof(string));
        dtSga.Rows.Add("Buffer Cache Size", 800L, "Yes");
        dtSga.Rows.Add("Shared Pool Size", 400L, "Yes");

        var sga = ServicioEsperasMemoria.ParsearSga(dtSga);
        Assert.Equal(2, sga.Count);
        Assert.Equal(800L, sga[0].Mb);
        Assert.Equal("Yes", sga[0].Redimensionable);

        using var dtPga = new DataTable();
        dtPga.Columns.Add("NAME", typeof(string));
        dtPga.Columns.Add("MB", typeof(long));
        dtPga.Rows.Add("total PGA allocated", 250L);
        dtPga.Rows.Add("total PGA inuse", 180L);

        var pga = ServicioEsperasMemoria.ParsearPga(dtPga);
        Assert.Equal(250L, pga["total PGA allocated"]);
        Assert.Equal(180L, pga["total PGA inuse"]);
    }

    [Fact]
    public void FiltrarComponentesParaGrafico_ExcluyeTotalesYValoresCero()
    {
        var componentes = new List<ComponenteMemoriaItem>
        {
            new("Maximum SGA Size", 1500L, "No"),
            new("Granule Size", 8L, "No"),
            new("Free SGA Memory Available", 100L, "No"),
            new("Unknown Aggregate", 1000L, "No"),
            new("Buffer Cache Size", 600L, "Yes"),
            new("Shared Pool Size", 300L, "Yes"),
            new("Streams Pool Size", 0L, "Yes")
        };

        var filtrados = ServicioEsperasMemoria.FiltrarComponentesParaGrafico(componentes);

        Assert.Equal(3, filtrados.Count);
        Assert.Equal("Buffer Cache Size", filtrados[0].Nombre);
        Assert.Equal("Shared Pool Size", filtrados[1].Nombre);
    }

    [Fact]
    public void ParsearOperacionesLargas_CalculaCamposCorrectamente()
    {
        using var dt = new DataTable();
        dt.Columns.Add("SID", typeof(int));
        dt.Columns.Add("OPNAME", typeof(string));
        dt.Columns.Add("TARGET", typeof(string));
        dt.Columns.Add("SOFAR", typeof(long));
        dt.Columns.Add("TOTALWORK", typeof(long));
        dt.Columns.Add("PCT", typeof(double));
        dt.Columns.Add("TIME_REMAINING", typeof(long));

        dt.Rows.Add(45, "Table Scan", "DEMO_VENTAS.FACTURA", 750L, 1000L, 75.0, 15L);

        var ops = ServicioEsperasMemoria.ParsearOperacionesLargas(dt);

        Assert.Single(ops);
        Assert.Equal(45, ops[0].Sid);
        Assert.Equal("Table Scan", ops[0].Operacion);
        Assert.Equal(75.0, ops[0].Porcentaje);
        Assert.Equal(15L, ops[0].TiempoRestante);
    }

    [Fact]
    public void ConsultasSqlPerformance_ContienenElementosIssue25()
    {
        Assert.Contains("v$system_event", SqlPerformance.EventosEspera);
        Assert.Contains("wait_class <> 'Idle'", SqlPerformance.EventosEspera);
        Assert.Contains("v$sgainfo", SqlPerformance.SgaInfo);
        Assert.Contains("v$pgastat", SqlPerformance.PgaStat);
        Assert.Contains("v$session_longops", SqlPerformance.OperacionesLargas);
        Assert.Contains("sofar < totalwork", SqlPerformance.OperacionesLargas);
    }
}

using System.Data;
using OraAdminXE.Modulos.Performance;
using Xunit;

namespace OraAdminXE.Tests;

public class ServicioMetricasTests
{
    [Theory]
    [InlineData(5, 5)]
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(0, 15)]
    [InlineData(10, 15)]
    [InlineData(-5, 15)]
    [InlineData(120, 15)]
    public void ValidarIntervalo_DevuelveIntervaloValido(int entrada, int esperado)
    {
        var resultado = ServicioMetricas.ValidarIntervalo(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ParsearMetricas_ConTablaVaciaONull_RetornaListaVacia()
    {
        Assert.Empty(ServicioMetricas.ParsearMetricas(null!));

        using var tablaVacia = new DataTable();
        Assert.Empty(ServicioMetricas.ParsearMetricas(tablaVacia));
    }

    [Fact]
    public void ParsearMetricas_ConFilas_MapeaCorrectamente()
    {
        using var dt = new DataTable();
        dt.Columns.Add("ID_METRICA", typeof(int));
        dt.Columns.Add("NOMBRE", typeof(string));
        dt.Columns.Add("UNIDAD", typeof(string));
        dt.Columns.Add("VALOR", typeof(decimal));

        dt.Rows.Add(1, "SESIONES_ACTIVAS", "sesiones", 2.45m);
        dt.Rows.Add(2, "CPU_POR_SEG", "cs/s", 15.20m);

        var resultado = ServicioMetricas.ParsearMetricas(dt);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(1, resultado[0].IdMetrica);
        Assert.Equal("SESIONES_ACTIVAS", resultado[0].Nombre);
        Assert.Equal("sesiones", resultado[0].Unidad);
        Assert.Equal(2.45, resultado[0].Valor, 2);

        Assert.Equal(2, resultado[1].IdMetrica);
        Assert.Equal("CPU_POR_SEG", resultado[1].Nombre);
    }

    [Fact]
    public void ParsearHistorial_MapeaFechasYValores()
    {
        using var dt = new DataTable();
        dt.Columns.Add("FECHA", typeof(DateTime));
        dt.Columns.Add("VALOR", typeof(double));

        var fecha1 = new DateTime(2026, 10, 28, 14, 0, 0);
        var fecha2 = new DateTime(2026, 10, 28, 14, 0, 15);

        dt.Rows.Add(fecha1, 10.5);
        dt.Rows.Add(fecha2, 12.8);

        var puntos = ServicioMetricas.ParsearHistorial(dt);

        Assert.Equal(2, puntos.Count);
        Assert.Equal(fecha1, puntos[0].Fecha);
        Assert.Equal(10.5, puntos[0].Valor);
        Assert.Equal(fecha2, puntos[1].Fecha);
        Assert.Equal(12.8, puntos[1].Valor);
    }

    [Fact]
    public void PrepararDatosGrafico_GeneraCoordenadasCorrectas()
    {
        var fecha = new DateTime(2026, 10, 28, 15, 30, 0);
        var puntos = new List<PuntoHistorial>
        {
            new(fecha, 42.0),
            new(fecha.AddSeconds(15), 55.5)
        };

        var (xs, ys) = ServicioMetricas.PrepararDatosGrafico(puntos);

        Assert.Equal(2, xs.Length);
        Assert.Equal(2, ys.Length);
        Assert.Equal(fecha.ToOADate(), xs[0]);
        Assert.Equal(42.0, ys[0]);
        Assert.Equal(fecha.AddSeconds(15).ToOADate(), xs[1]);
        Assert.Equal(55.5, ys[1]);
    }

    [Fact]
    public void FormatearNombre_TraduceNombresDelCatalogo()
    {
        Assert.Equal("Sesiones Activas Promedio", ServicioMetricas.FormatearNombre("SESIONES_ACTIVAS"));
        Assert.Equal("CPU", ServicioMetricas.FormatearNombre("CPU_POR_SEG"));
        Assert.Equal("Lecturas Lógicas", ServicioMetricas.FormatearNombre("LECTURAS_LOGICAS"));
        Assert.Equal("Ejecuciones", ServicioMetricas.FormatearNombre("EJECUCIONES_SEG"));
        Assert.Equal("Transacciones", ServicioMetricas.FormatearNombre("TRANSACCIONES_SEG"));
        Assert.Equal("OTRA_METRICA", ServicioMetricas.FormatearNombre("OTRA_METRICA"));
    }

    [Fact]
    public void SqlPerformance_ContieneConsultasEsperadasIssue24()
    {
        Assert.Contains("v$con_sysmetric", SqlPerformance.MetricasActuales);
        Assert.Contains("group_id = 18", SqlPerformance.MetricasActuales);
        Assert.Contains("RETURNING id_snapshot INTO :id", SqlPerformance.InsertarSnapshot);
        Assert.Contains("INSERT INTO admin_bd.valor_metrica", SqlPerformance.InsertarValoresMetricas);
        Assert.Contains("INTERVAL '1' HOUR", SqlPerformance.HistorialMetrica);
        Assert.Contains("DELETE FROM admin_bd.snapshot", SqlPerformance.BorrarHistorial);
    }
}

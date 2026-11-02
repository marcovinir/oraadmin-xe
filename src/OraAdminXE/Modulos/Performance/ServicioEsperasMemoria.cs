using System.Data;

namespace OraAdminXE.Modulos.Performance;

public sealed record EventoEsperaItem(string Evento, string Clase, long TotalWaits, double Segundos);

public sealed record TopSqlItem(
    string SqlId,
    long Ejecuciones,
    double Segundos,
    double CpuSegundos,
    long BufferGets,
    long DiskReads,
    string Texto);

public sealed record ComponenteMemoriaItem(string Nombre, long Mb, string? Redimensionable);

public sealed record OperacionLargaItem(
    int Sid,
    string Operacion,
    string Destino,
    long SoFar,
    long TotalWork,
    double Porcentaje,
    long TiempoRestante);

/// <summary>
/// Lógica de análisis de esperas, top SQL, memoria SGA/PGA y operaciones largas (issue #25).
/// </summary>
public static class ServicioEsperasMemoria
{
    public static readonly (string Codigo, string Descripcion)[] CriteriosTopSql =
    [
        ("elapsed_time", "Tiempo transcurrido"),
        ("cpu_time", "Tiempo de CPU"),
        ("buffer_gets", "Lecturas lógicas (búfer)"),
        ("disk_reads", "Lecturas de disco")
    ];

    public static string ValidarCriterioTopSql(string? criterio) => criterio switch
    {
        "cpu_time" => "cpu_time",
        "buffer_gets" => "buffer_gets",
        "disk_reads" => "disk_reads",
        _ => "elapsed_time"
    };

    public static List<EventoEsperaItem> ParsearEventos(DataTable? tabla)
    {
        var lista = new List<EventoEsperaItem>();
        if (tabla == null) return lista;

        foreach (DataRow fila in tabla.Rows)
        {
            var evento = fila["EVENT"]?.ToString() ?? "";
            var clase = fila["WAIT_CLASS"]?.ToString() ?? "";
            var totalWaits = fila["TOTAL_WAITS"] is not DBNull ? Convert.ToInt64(fila["TOTAL_WAITS"]) : 0L;
            var seg = fila["SEG"] is not DBNull ? Convert.ToDouble(fila["SEG"]) : 0.0;
            lista.Add(new EventoEsperaItem(evento, clase, totalWaits, seg));
        }

        return lista;
    }

    public static List<TopSqlItem> ParsearTopSql(DataTable? tabla)
    {
        var lista = new List<TopSqlItem>();
        if (tabla == null) return lista;

        foreach (DataRow fila in tabla.Rows)
        {
            var sqlId = fila["SQL_ID"]?.ToString() ?? "";
            var ejecuciones = fila["EXECUTIONS"] is not DBNull ? Convert.ToInt64(fila["EXECUTIONS"]) : 0L;
            var seg = fila["SEG"] is not DBNull ? Convert.ToDouble(fila["SEG"]) : 0.0;
            var cpuSeg = fila["CPU_SEG"] is not DBNull ? Convert.ToDouble(fila["CPU_SEG"]) : 0.0;
            var bufferGets = fila["BUFFER_GETS"] is not DBNull ? Convert.ToInt64(fila["BUFFER_GETS"]) : 0L;
            var diskReads = fila["DISK_READS"] is not DBNull ? Convert.ToInt64(fila["DISK_READS"]) : 0L;
            var texto = fila["TEXTO"]?.ToString() ?? "";
            lista.Add(new TopSqlItem(sqlId, ejecuciones, seg, cpuSeg, bufferGets, diskReads, texto));
        }

        return lista;
    }

    public static List<ComponenteMemoriaItem> ParsearSga(DataTable? tabla)
    {
        var lista = new List<ComponenteMemoriaItem>();
        if (tabla == null) return lista;

        foreach (DataRow fila in tabla.Rows)
        {
            var nombre = fila["NAME"]?.ToString() ?? "";
            var mb = fila["MB"] is not DBNull ? Convert.ToInt64(fila["MB"]) : 0L;
            var resizeable = fila["RESIZEABLE"]?.ToString();
            lista.Add(new ComponenteMemoriaItem(nombre, mb, resizeable));
        }

        return lista;
    }

    public static Dictionary<string, long> ParsearPga(DataTable? tabla)
    {
        var dic = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (tabla == null) return dic;

        foreach (DataRow fila in tabla.Rows)
        {
            var nombre = fila["NAME"]?.ToString() ?? "";
            var mb = fila["MB"] is not DBNull ? Convert.ToInt64(fila["MB"]) : 0L;
            dic[nombre] = mb;
        }

        return dic;
    }

    public static List<OperacionLargaItem> ParsearOperacionesLargas(DataTable? tabla)
    {
        var lista = new List<OperacionLargaItem>();
        if (tabla == null) return lista;

        foreach (DataRow fila in tabla.Rows)
        {
            var sid = fila["SID"] is not DBNull ? Convert.ToInt32(fila["SID"]) : 0;
            var opname = fila["OPNAME"]?.ToString() ?? "";
            var target = fila["TARGET"]?.ToString() ?? "";
            var sofar = fila["SOFAR"] is not DBNull ? Convert.ToInt64(fila["SOFAR"]) : 0L;
            var totalwork = fila["TOTALWORK"] is not DBNull ? Convert.ToInt64(fila["TOTALWORK"]) : 0L;
            var pct = fila["PCT"] is not DBNull ? Convert.ToDouble(fila["PCT"]) : 0.0;
            var remaining = fila["TIME_REMAINING"] is not DBNull ? Convert.ToInt64(fila["TIME_REMAINING"]) : 0L;
            lista.Add(new OperacionLargaItem(sid, opname, target, sofar, totalwork, pct, remaining));
        }

        return lista;
    }

    /// <summary>
    /// Filtra los componentes desglosados de la SGA que tienen tamaño positivo para el gráfico de torta
    /// (excluyendo totales agregados como Maximum SGA Size o Granule Size).
    /// </summary>
    public static List<ComponenteMemoriaItem> FiltrarComponentesParaGrafico(IEnumerable<ComponenteMemoriaItem> componentes)
    {
        // v$sgainfo incluye límites y subtotales que no son componentes independientes.
        var componentesSga = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Fixed SGA Size", "Redo Buffers", "Buffer Cache Size", "Shared Pool Size",
            "Large Pool Size", "Java Pool Size", "Streams Pool Size", "Shared IO Pool Size",
            "Data Transfer Cache Size", "Free SGA Memory Available"
        };

        return componentes
            .Where(c => c.Mb > 0 && componentesSga.Contains(c.Nombre))
            .OrderByDescending(c => c.Mb)
            .ToList();
    }
}

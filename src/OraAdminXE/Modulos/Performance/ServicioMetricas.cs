using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Métrica de rendimiento obtenida de v$con_sysmetric y del catálogo METRICA (issue #24).
/// </summary>
public sealed record MetricaItem(int IdMetrica, string Nombre, string Unidad, double Valor);

/// <summary>
/// Muestra histórica de una métrica en un punto en el tiempo (issue #24).
/// </summary>
public sealed record PuntoHistorial(DateTime Fecha, double Valor);

/// <summary>
/// Lógica de captura y consulta de métricas en vivo (issue #24).
/// </summary>
public static class ServicioMetricas
{
    public static readonly int[] IntervalosValidos = [5, 15, 30, 60];

    /// <summary>Valida el intervalo en segundos (5, 15, 30, 60); retorna 15 por defecto si no es válido.</summary>
    public static int ValidarIntervalo(int segundos) =>
        Array.Exists(IntervalosValidos, s => s == segundos) ? segundos : 15;

    /// <summary>Convierte el DataTable de métricas actuales en una lista de objetos tipados.</summary>
    public static List<MetricaItem> ParsearMetricas(DataTable tabla)
    {
        var lista = new List<MetricaItem>();
        if (tabla == null) return lista;

        foreach (DataRow fila in tabla.Rows)
        {
            var id = fila["ID_METRICA"] is not DBNull ? Convert.ToInt32(fila["ID_METRICA"]) : 0;
            var nombre = fila["NOMBRE"]?.ToString() ?? "";
            var unidad = fila["UNIDAD"]?.ToString() ?? "";
            var valor = fila["VALOR"] is not DBNull ? Convert.ToDouble(fila["VALOR"]) : 0.0;
            lista.Add(new MetricaItem(id, nombre, unidad, valor));
        }

        return lista;
    }

    /// <summary>Convierte el DataTable de historial en una lista de puntos cronológicos.</summary>
    public static List<PuntoHistorial> ParsearHistorial(DataTable tabla)
    {
        var lista = new List<PuntoHistorial>();
        if (tabla == null) return lista;

        foreach (DataRow fila in tabla.Rows)
        {
            if (fila["FECHA"] is not DBNull and not null &&
                fila["VALOR"] is not DBNull and not null)
            {
                var fecha = Convert.ToDateTime(fila["FECHA"]);
                var valor = Convert.ToDouble(fila["VALOR"]);
                lista.Add(new PuntoHistorial(fecha, valor));
            }
        }

        return lista;
    }

    /// <summary>Convierte la lista de puntos a coordenadas (OA Date en X, Valor en Y) para ScottPlot.</summary>
    public static (double[] Xs, double[] Ys) PrepararDatosGrafico(IReadOnlyList<PuntoHistorial> puntos)
    {
        if (puntos == null || puntos.Count == 0)
            return (Array.Empty<double>(), Array.Empty<double>());

        var xs = new double[puntos.Count];
        var ys = new double[puntos.Count];

        for (int i = 0; i < puntos.Count; i++)
        {
            xs[i] = puntos[i].Fecha.ToOADate();
            ys[i] = puntos[i].Valor;
        }

        return (xs, ys);
    }

    /// <summary>Formatea el nombre del catálogo a un texto legible para tarjetas y títulos.</summary>
    public static string FormatearNombre(string nombre) => nombre switch
    {
        "SESIONES_ACTIVAS" => "Sesiones Activas Promedio",
        "CPU_POR_SEG" => "CPU",
        "LECTURAS_LOGICAS" => "Lecturas Lógicas",
        "EJECUCIONES_SEG" => "Ejecuciones",
        "TRANSACCIONES_SEG" => "Transacciones",
        _ => nombre
    };

    /// <summary>
    /// Toma una muestra de métricas insertando en SNAPSHOT y VALOR_METRICA sobre la misma conexión.
    /// No se registra en la bitácora para evitar ruido recurrente.
    /// </summary>
    public static async Task<long> RegistrarMuestraAsync(IBaseDatos bd)
    {
        return await bd.EnMismaSesionAsync(async conexion =>
        {
            using var transaccion = conexion.BeginTransaction();
            try
            {
            long idSnapshot;
            await using (var cmdSnap = new OracleCommand(SqlPerformance.InsertarSnapshot, conexion) { BindByName = true })
            {
                var pId = new OracleParameter("id", OracleDbType.Decimal, ParameterDirection.Output);
                cmdSnap.Parameters.Add(pId);
                await cmdSnap.ExecuteNonQueryAsync();

                idSnapshot = Convert.ToInt64(((Oracle.ManagedDataAccess.Types.OracleDecimal)pId.Value).Value);
            }

            await using (var cmdVal = new OracleCommand(SqlPerformance.InsertarValoresMetricas, conexion) { BindByName = true })
            {
                cmdVal.Parameters.Add(new OracleParameter("id", idSnapshot));
                if (await cmdVal.ExecuteNonQueryAsync() == 0)
                    throw new InvalidOperationException("No hay métricas disponibles para registrar la muestra.");
            }

            transaccion.Commit();
            return idSnapshot;
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        });
    }

    /// <summary>Borra todo el historial de snapshots (en cascada elimina los valores). Destructiva.</summary>
    public static async Task<int> BorrarHistorialAsync(IBaseDatos bd)
    {
        return await bd.EjecutarAsync(Modulo.Performance, "Borrar historial de métricas", SqlPerformance.BorrarHistorial);
    }
}

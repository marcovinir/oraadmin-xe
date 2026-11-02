using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Tuning;

/// <summary>Resultado de pedir un plan de ejecución. Lo que no aplica al tipo de plan queda en null.</summary>
public sealed record ResultadoPlan(
    string Texto,
    decimal? Costo,
    decimal? FilasEstimadas,
    string? Operacion,
    long? FilasDevueltas,
    TimeSpan? Tiempo);

/// <summary>Comparación de costos antes/después: <see cref="Valor"/> «173 → 107», <see cref="Detalle"/> «−38 %».</summary>
public sealed record ResumenCosto(string Valor, string Detalle, bool Mejora, bool Empeora);

/// <summary>
/// Planes de ejecución estimado (EXPLAIN PLAN) y real (GATHER_PLAN_STATISTICS + DISPLAY_CURSOR).
/// Ambos necesitan que todas las sentencias corran en la MISMA sesión de Oracle,
/// por eso usan <see cref="IBaseDatos.EnMismaSesionAsync{T}"/>.
/// </summary>
public static class ServicioPlanes
{
    private const string Hint = "GATHER_PLAN_STATISTICS";

    private static readonly Regex InicioConsulta = new(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Valida que sea una consulta (SELECT o WITH) y quita espacios y el «;» final.
    /// Lanza <see cref="ArgumentException"/>, que la UI muestra como advertencia.
    /// </summary>
    public static string Validar(string? consulta)
    {
        var limpia = (consulta ?? "").Trim().TrimEnd(';', ' ', '\r', '\n', '\t');
        if (limpia.Length == 0)
            throw new ArgumentException("Escriba una consulta para analizar.");
        if (!InicioConsulta.IsMatch(limpia))
            throw new ArgumentException("Solo se permiten consultas que empiecen con SELECT o WITH.");
        return limpia;
    }

    /// <summary>
    /// Inserta el hint GATHER_PLAN_STATISTICS después del SELECT principal (el primero fuera de paréntesis,
    /// así en un WITH no cae dentro de la subconsulta). Si ya hay un hint, agrega este al principio.
    /// </summary>
    public static string ConHint(string consulta)
    {
        var pos = BuscarSelectPrincipal(consulta);
        if (pos < 0) return consulta;

        var despues = pos + "SELECT".Length;
        var resto = consulta[despues..];
        // Si ya hay un hint se agrega al principio sin comprobar si ya está: Oracle ignora un hint repetido,
        // y detectarlo con texto es frágil (NO_GATHER_…, FULL(GATHER_…), LEADING((a b) …)).
        var hintExistente = Regex.Match(resto, @"^\s*/\*\+");
        if (hintExistente.Success)
        {
            var fin = despues + hintExistente.Length;
            return consulta[..fin] + " " + Hint + consulta[fin..];
        }
        return consulta[..despues] + $" /*+ {Hint} */" + consulta[despues..];
    }

    /// <summary>Posición de la palabra SELECT de nivel 0 (fuera de paréntesis, cadenas y comentarios), o -1.</summary>
    private static int BuscarSelectPrincipal(string sql)
    {
        var profundidad = 0;
        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c == '\'')
            {
                i++;
                while (i < sql.Length && !(sql[i] == '\'' && (i + 1 >= sql.Length || sql[i + 1] != '\''))) i += sql[i] == '\'' ? 2 : 1;
            }
            else if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var fin = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = fin < 0 ? sql.Length : fin + 1;
            }
            else if (c == '(') profundidad++;
            else if (c == ')') profundidad--;
            else if (profundidad == 0 && (c is 'S' or 's') &&
                     string.Compare(sql, i, "SELECT", 0, 6, StringComparison.OrdinalIgnoreCase) == 0 &&
                     (i == 0 || !EsIdentificador(sql[i - 1])) &&
                     (i + 6 >= sql.Length || !EsIdentificador(sql[i + 6])))
                return i;
        }
        return -1;
    }

    private static bool EsIdentificador(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' or '#';

    /// <summary>
    /// Primera operación de acceso a datos del plan (TABLE ACCESS … / INDEX …) con el objeto que toca,
    /// por ejemplo «TABLE ACCESS FULL · DETALLE_FACTURA». Null si el plan no tiene ninguna.
    /// </summary>
    public static string? OperacionPrincipal(string textoPlan)
    {
        var linea = LineaOperacionPrincipal(textoPlan);
        if (linea is null) return null;
        var celdas = linea.Split('|');
        // | Id | Operation | Name | …  →  celdas[0] vacía, [1] Id, [2] Operation, [3] Name
        if (celdas.Length < 4) return linea.Trim();
        var operacion = celdas[2].Trim();
        var objeto = celdas[3].Trim();
        return objeto.Length > 0 ? $"{operacion} · {objeto}" : operacion;
    }

    /// <summary>Línea del plan de <see cref="OperacionPrincipal"/>, para resaltarla en pantalla.</summary>
    public static string? LineaOperacionPrincipal(string textoPlan) =>
        textoPlan.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.StartsWith('|') && Regex.IsMatch(l, @"\|\s*\*?\s*\d+\s*\|\s*(TABLE ACCESS|INDEX|MAT_VIEW ACCESS)\b"));

    /// <summary>Plan estimado: EXPLAIN PLAN + DBMS_XPLAN.DISPLAY + costo, y limpia PLAN_TABLE al terminar.</summary>
    public static async Task<ResultadoPlan> EstimarAsync(IBaseDatos bd, string consulta)
    {
        var id = "OAX_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var sql = SqlTuning.ExplicarPlan(id, consulta);
        try
        {
            var resultado = await bd.EnMismaSesionAsync(async conexion =>
            {
                ResultadoPlan plan;
                try
                {
                    await Ejecutar(conexion, sql);
                    var texto = await LeerTexto(conexion, SqlTuning.PlanEstimado, Parametro.De("id", id));

                    decimal? costo = null, filas = null;
                    await using var cmd = Comando(conexion, SqlTuning.CostoDelPlan, Parametro.De("id", id));
                    await using var lector = await cmd.ExecuteReaderAsync();
                    if (await lector.ReadAsync())
                    {
                        costo = lector.IsDBNull(0) ? null : Convert.ToDecimal(lector.GetValue(0));
                        filas = lector.IsDBNull(1) ? null : Convert.ToDecimal(lector.GetValue(1));
                    }
                    plan = new ResultadoPlan(texto, costo, filas, OperacionPrincipal(texto), null, null);
                }
                catch
                {
                    // PLAN_TABLE es compartida: se limpia lo escrito, sin tapar el error original.
                    try { await Ejecutar(conexion, SqlTuning.LimpiarPlan, Parametro.De("id", id)); }
                    catch (OracleException) { }
                    throw;
                }
                // Si todo salió bien y la limpieza falla, el error sí se informa.
                await Ejecutar(conexion, SqlTuning.LimpiarPlan, Parametro.De("id", id));
                return plan;
            });
            await bd.RegistrarBitacoraAsync(Modulo.Tuning, "Plan estimado", sql, true, $"Costo {resultado.Costo}");
            return resultado;
        }
        catch (Exception ex)
        {
            await bd.RegistrarBitacoraAsync(Modulo.Tuning, "Plan estimado", sql, false, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Plan real: ejecuta la consulta con GATHER_PLAN_STATISTICS, lee TODAS las filas
    /// (si no, DISPLAY_CURSOR no tiene las estadísticas completas) y pide el plan de esa sentencia.
    /// </summary>
    public static async Task<ResultadoPlan> EjecutarAsync(IBaseDatos bd, string consulta)
    {
        var sql = ConHint(consulta);
        try
        {
            var resultado = await bd.EnMismaSesionAsync(async conexion =>
            {
                long filas = 0;
                var reloj = Stopwatch.StartNew();
                await using (var cmd = Comando(conexion, sql))
                await using (var lector = await cmd.ExecuteReaderAsync())
                    while (await lector.ReadAsync()) filas++;
                reloj.Stop();

                var texto = await LeerTexto(conexion, SqlTuning.PlanReal);
                return new ResultadoPlan(texto, null, null, OperacionPrincipal(texto), filas, reloj.Elapsed);
            });
            await bd.RegistrarBitacoraAsync(Modulo.Tuning, "Plan real", sql, true,
                $"{resultado.FilasDevueltas} filas en {resultado.Tiempo?.TotalSeconds:0.00} s");
            return resultado;
        }
        catch (Exception ex)
        {
            await bd.RegistrarBitacoraAsync(Modulo.Tuning, "Plan real", sql, false, ex.Message);
            throw;
        }
    }

    /// <summary>Texto completo (CLOB) de una sentencia de v$sqlarea, para pasarla al editor sin truncar.</summary>
    public static Task<string?> TextoCompletoAsync(IBaseDatos bd, string sqlId) =>
        bd.EnMismaSesionAsync(async conexion =>
        {
            await using var cmd = Comando(conexion, SqlTuning.TextoCompleto, Parametro.De("id", sqlId));
            cmd.InitialLOBFetchSize = -1;   // trae el CLOB entero en la consulta
            await using var lector = await cmd.ExecuteReaderAsync();
            return await lector.ReadAsync() && !lector.IsDBNull(0) ? lector.GetString(0) : null;
        });

    /// <summary>
    /// Compara el costo del plan ANTES con el de DESPUÉS. Null si alguno no tiene costo (los planes reales no lo traen).
    /// </summary>
    public static ResumenCosto? CompararCostos(ResultadoPlan antes, ResultadoPlan despues)
    {
        if (antes.Costo is not { } a || despues.Costo is not { } d) return null;
        var valor = $"{a:0} → {d:0}";
        if (d == a) return new ResumenCosto(valor, "sin cambio", false, false);
        if (a == 0) return new ResumenCosto(valor, d > a ? "sube" : "baja", d < a, d > a);
        var pct = Math.Round((d - a) / a * 100m, MidpointRounding.AwayFromZero);
        var texto = pct == 0 ? (d < a ? "−<1 %" : "+<1 %")
            : pct < 0 ? $"−{-pct:0} %" : $"+{pct:0} %";
        return new ResumenCosto(valor, texto, d < a, d > a);
    }

    private static OracleCommand Comando(OracleConnection conexion, string sql, params OracleParameter[] parametros)
    {
        var cmd = new OracleCommand(sql, conexion) { BindByName = true, CommandTimeout = 0 };
        cmd.Parameters.AddRange(parametros);
        return cmd;
    }

    private static async Task Ejecutar(OracleConnection conexion, string sql, params OracleParameter[] parametros)
    {
        await using var cmd = Comando(conexion, sql, parametros);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Une las líneas de una consulta de una sola columna (la salida de DBMS_XPLAN).</summary>
    private static async Task<string> LeerTexto(OracleConnection conexion, string sql, params OracleParameter[] parametros)
    {
        var texto = new StringBuilder();
        await using var cmd = Comando(conexion, sql, parametros);
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            texto.AppendLine(lector.IsDBNull(0) ? "" : lector.GetString(0));
        return texto.ToString().TrimEnd();
    }
}

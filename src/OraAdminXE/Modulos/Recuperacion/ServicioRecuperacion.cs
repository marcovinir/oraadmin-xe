using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Respaldos;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>Qué parte del respaldo se restaura.</summary>
public enum AlcanceRestauracion { Todo, Esquemas, Tablas }

/// <summary>TABLE_EXISTS_ACTION de Data Pump: qué hacer si la tabla ya existe.</summary>
public enum AccionExistente { Skip, Append, Truncate, Replace }

/// <summary>Un respaldo COMPLETADO del catálogo, con lo necesario para restaurarlo.</summary>
public sealed record RespaldoRestaurable(
    int Id, TipoRespaldo Tipo, string Directorio, string RutaSo, string ArchivoDump, string ArchivoLog,
    IReadOnlyList<(string Esquema, string? Tabla)> Objetos)
{
    public string RutaDump => Path.Combine(RutaSo, ArchivoDump);
    public string RutaLog => Path.Combine(RutaSo, ArchivoLog);

    /// <summary>Windows no distingue mayúsculas (Data Pump guarda el .DMP en mayúsculas).</summary>
    public bool ExisteEnDisco => File.Exists(RutaDump);

    /// <summary>Esquemas del respaldo: los del catálogo o, en un full, los que trae su log.</summary>
    public IReadOnlyList<string> Esquemas(IEnumerable<string>? excluir = null)
    {
        var fuera = (excluir ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nombres = Objetos.Count > 0 ? Objetos.Select(o => o.Esquema) : LogDataPump.TablasDeArchivo(RutaLog).Select(t => t.Esquema);
        return nombres.Distinct().Where(e => !fuera.Contains(e)).OrderBy(e => e).ToList();
    }
}

/// <summary>Lo que el usuario eligió en la pantalla de Recuperación.</summary>
public sealed record SolicitudRestauracion(
    RespaldoRestaurable Respaldo,
    AlcanceRestauracion Alcance,
    IReadOnlyList<string> Esquemas,
    IReadOnlyList<string> Tablas,
    AccionExistente Accion,
    string? EsquemaDestino,
    DateTime Fecha)
{
    /// <summary>job_mode del import: el del respaldo si se restaura todo; si no, SCHEMA o TABLE.</summary>
    public string Modo => Alcance switch
    {
        AlcanceRestauracion.Esquemas => "SCHEMA",
        AlcanceRestauracion.Tablas => "TABLE",
        _ => Respaldo.Tipo.ToString().ToUpperInvariant()
    };

    public string Job => $"IMP_{Modo}_{Fecha:yyyyMMdd_HHmmss}";
    public string ArchivoLog => Job + ".LOG";
    public string CodigoAccion => Accion.ToString().ToUpperInvariant();

    /// <summary>REPLACE y TRUNCATE borran datos que hoy existen: piden la confirmación destructiva.</summary>
    public bool Destructiva => Accion is AccionExistente.Replace or AccionExistente.Truncate;

    /// <summary>Esquema de origen cuando hay uno solo (necesario para REMAP_SCHEMA).</summary>
    public string? EsquemaOrigen => Alcance switch
    {
        AlcanceRestauracion.Todo => Respaldo.Objetos.Select(o => o.Esquema).Distinct().Count() == 1 ? Respaldo.Objetos[0].Esquema : null,
        _ => Esquemas.Count == 1 ? Esquemas[0] : null
    };
}

/// <summary>Cómo terminó una restauración y cuántas filas quedaron en cada tabla.</summary>
public sealed record ResultadoRestauracion(int IdRestauracion, string EstadoJob, int ErroresLog, int TablasOmitidas, IReadOnlyList<(string Tabla, long? Filas)> Conteos)
{
    public bool Completado => EstadoJob == "COMPLETED";
}

/// <summary>
/// Recuperación con DBMS_DATAPUMP (issue #13): arma el bloque de import y ejecuta el flujo completo
/// (RESTAURACION / RESTAURACION_OBJETO → import → revisión del log → estado final → bitácora → conteo de filas).
/// </summary>
public sealed class ServicioRecuperacion(IBaseDatos bd)
{
    /// <summary>Máximo de tablas que se cuentan al terminar (un full puede traer cientos).</summary>
    private const int MaxConteos = 30;

    public static void Validar(SolicitudRestauracion s)
    {
        if (!s.Respaldo.ExisteEnDisco)
            throw new ArgumentException($"El archivo {s.Respaldo.ArchivoDump} ya no está en {s.Respaldo.RutaSo}: este respaldo no se puede restaurar.");
        if (s.Alcance == AlcanceRestauracion.Esquemas && s.Esquemas.Count == 0)
            throw new ArgumentException("Marque al menos un esquema para restaurar.");
        if (s.Alcance == AlcanceRestauracion.Esquemas && s.Respaldo.Tipo == TipoRespaldo.Table)
            throw new ArgumentException("Un respaldo por tabla no trae esquemas completos: restaure todo o algunas tablas.");
        if (s.Alcance == AlcanceRestauracion.Tablas && s.Esquemas.Count != 1)
            throw new ArgumentException("Elija el esquema de las tablas.");
        if (s.Alcance == AlcanceRestauracion.Tablas && s.Tablas.Count == 0)
            throw new ArgumentException("Marque al menos una tabla para restaurar.");
        foreach (var e in s.Esquemas) Identificador.Validar(e, "esquema");
        foreach (var t in s.Tablas) Identificador.Validar(t, "tabla");
        if (s.EsquemaDestino is not null)
        {
            var destino = Identificador.Validar(s.EsquemaDestino, "esquema destino");
            if (s.EsquemaOrigen is not { } origen)
                throw new ArgumentException("Para restaurar en otro esquema elija un solo esquema de origen.");
            if (origen == destino)
                throw new ArgumentException("El esquema destino es el mismo que el de origen: desmarque «Restaurar en otro esquema».");
        }
    }

    /// <summary>El bloque PL/SQL exacto que se ejecuta (es el mismo que muestra «Ver SQL»).</summary>
    public static string ArmarBloque(SolicitudRestauracion s)
    {
        Validar(s);
        var configuracion = new List<string>();
        if (s.Alcance != AlcanceRestauracion.Todo) configuracion.Add(BloqueDataPump.FiltroEsquemas(s.Esquemas));
        if (s.Alcance == AlcanceRestauracion.Tablas) configuracion.Add(BloqueDataPump.FiltroTablas(s.Tablas));
        configuracion.Add($"DBMS_DATAPUMP.SET_PARAMETER(h, 'TABLE_EXISTS_ACTION', '{s.CodigoAccion}');");
        if (s.EsquemaDestino is not null)
            configuracion.Add($"DBMS_DATAPUMP.METADATA_REMAP(h, 'REMAP_SCHEMA', '{Identificador.Validar(s.EsquemaOrigen!)}', '{Identificador.Validar(s.EsquemaDestino)}');");

        return BloqueDataPump.Armar("IMPORT", s.Modo, s.Job, Identificador.Validar(s.Respaldo.Directorio, "directorio"),
            s.Respaldo.ArchivoDump, s.ArchivoLog, configuracion);
    }

    /// <summary>Objetos que se guardan en RESTAURACION_OBJETO (los del origen, como se eligieron).</summary>
    public static IReadOnlyList<(string Esquema, string? Tabla)> Objetos(SolicitudRestauracion s) => s.Alcance switch
    {
        AlcanceRestauracion.Esquemas => s.Esquemas.Select(e => (Identificador.Validar(e), (string?)null)).ToList(),
        AlcanceRestauracion.Tablas => s.Tablas.Select(t => (Identificador.Validar(s.Esquemas[0]), (string?)Identificador.Validar(t))).ToList(),
        _ => s.Respaldo.Objetos
    };

    /// <summary>Filas que tiene hoy cada tabla (null si no existe). Los nombres se validan antes de armar el COUNT(*).</summary>
    public async Task<IReadOnlyList<(string Tabla, long? Filas)>> ContarAsync(IEnumerable<(string Esquema, string Tabla)> tablas)
    {
        var conteos = new List<(string, long?)>();
        foreach (var (esquema, tabla) in tablas.Take(MaxConteos))
        {
            var e = Identificador.Validar(esquema, "esquema");
            var t = Identificador.Validar(tabla, "tabla");
            var existe = Convert.ToInt32(await bd.EscalarAsync(SqlRecuperacion.ExisteTabla, Parametro.De("e", e), Parametro.De("t", t))) > 0;
            conteos.Add(($"{e}.{t}", existe ? Convert.ToInt64(await bd.EscalarAsync($"SELECT COUNT(*) FROM {e}.\"{t}\"")) : null));
        }
        return conteos;
    }

    /// <summary>Las tablas que se verán afectadas, ya con el esquema destino (para contarlas antes y después).</summary>
    public static IReadOnlyList<(string Esquema, string Tabla)> TablasAfectadas(SolicitudRestauracion s)
    {
        var destino = s.EsquemaDestino is null ? null : Identificador.Validar(s.EsquemaDestino);
        IEnumerable<(string Esquema, string Tabla)> origen = s.Alcance switch
        {
            AlcanceRestauracion.Tablas => s.Tablas.Select(t => (s.Esquemas[0], t)),
            AlcanceRestauracion.Esquemas => LogDataPump.TablasDeArchivo(s.Respaldo.RutaLog)
                .Where(t => s.Esquemas.Contains(t.Esquema, StringComparer.OrdinalIgnoreCase)).Select(t => (t.Esquema, t.Tabla)),
            _ => s.Respaldo.Tipo == TipoRespaldo.Full ? [] : LogDataPump.TablasDeArchivo(s.Respaldo.RutaLog).Select(t => (t.Esquema, t.Tabla))
        };
        return origen.Select(t => (destino ?? Identificador.Validar(t.Esquema), Identificador.Validar(t.Tabla))).ToList();
    }

    /// <summary>
    /// Ejecuta la restauración en una sola sesión. Un import puede terminar COMPLETED pero con errores en el log
    /// (objetos que ya existen, por ejemplo): se cuentan las líneas ORA- y se guardan en la observación.
    /// </summary>
    public Task<ResultadoRestauracion> EjecutarAsync(SolicitudRestauracion s)
    {
        var bloque = ArmarBloque(s);
        var accion = $"Restaurar {s.Modo}";
        return Task.Run(async () =>
        {
            var (id, estadoJob, errores, omitidas) = await bd.EnMismaSesionAsync(async conexion =>
            {
                var id = await RegistrarAsync(conexion, s);
                try
                {
                    var estado = new OracleParameter("estado", OracleDbType.Varchar2, 30) { Direction = ParameterDirection.Output };
                    await Ejecutar(conexion, bloque, estado);
                    var estadoJob = estado.Value?.ToString() ?? "";
                    var (errores, omitidas) = RevisarLog(Path.Combine(s.Respaldo.RutaSo, s.ArchivoLog));
                    var avisos = string.Join(", ", new[]
                    {
                        errores > 0 ? $"{errores} línea(s) ORA- en el log" : null,
                        omitidas > 0 ? $"{omitidas} tabla(s) omitida(s) porque ya existían (SKIP)" : null
                    }.OfType<string>());
                    var obs = estadoJob != "COMPLETED" ? $"El job terminó en estado {estadoJob}" : avisos.Length > 0 ? avisos : null;
                    await Ejecutar(conexion, SqlRecuperacion.Terminar,
                        Parametro.De("estado", estadoJob == "COMPLETED" ? "COMPLETADO" : "ERROR"), Parametro.De("obs", obs), Parametro.De("id", id));
                    await bd.RegistrarBitacoraAsync(Modulo.Recuperacion, accion, bloque, estadoJob == "COMPLETED",
                        $"{s.Respaldo.ArchivoDump} → {s.ArchivoLog}: {estadoJob}" + (avisos.Length > 0 ? $", {avisos}" : ""));
                    return (id, estadoJob, errores, omitidas);
                }
                catch (Exception ex)
                {
                    try { await Ejecutar(conexion, SqlRecuperacion.MarcarError, Parametro.De("mensaje", ex.Message), Parametro.De("id", id)); }
                    catch (OracleException) { /* el error original es el que importa */ }
                    await bd.RegistrarBitacoraAsync(Modulo.Recuperacion, accion, bloque, false, ex.Message);
                    throw;
                }
            });
            var conteos = await ContarAsync(TablasAfectadas(s));
            return new ResultadoRestauracion(id, estadoJob, errores, omitidas, conteos);
        });
    }

    /// <summary>Líneas con ORA- y tablas omitidas por SKIP en el log del import.</summary>
    private static (int Errores, int Omitidas) RevisarLog(string ruta)
    {
        try
        {
            var contenido = File.ReadAllText(ruta);
            return (DlgLog.Errores(contenido), LogDataPump.Omitidas(contenido));
        }
        catch (IOException) { return (0, 0); }
    }

    private static async Task<int> RegistrarAsync(OracleConnection conexion, SolicitudRestauracion s)
    {
        var id = new OracleParameter("id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
        await Ejecutar(conexion, SqlRecuperacion.InsertarRestauracion,
            Parametro.De("respaldo", s.Respaldo.Id), Parametro.De("job", s.Job),
            Parametro.De("destino", s.EsquemaDestino is null ? null : Identificador.Validar(s.EsquemaDestino)),
            Parametro.De("accion", s.CodigoAccion), Parametro.De("log", s.ArchivoLog), id);
        var idRestauracion = Convert.ToInt32(id.Value.ToString());
        foreach (var (esquema, tabla) in Objetos(s))
            await Ejecutar(conexion, SqlRecuperacion.InsertarObjeto,
                Parametro.De("id", idRestauracion), Parametro.De("esquema", esquema), Parametro.De("tabla", tabla));
        return idRestauracion;
    }

    private static async Task Ejecutar(OracleConnection conexion, string sql, params OracleParameter[] parametros)
    {
        await using var cmd = new OracleCommand(sql, conexion) { BindByName = true, CommandTimeout = 0 };
        cmd.Parameters.AddRange(parametros);
        await cmd.ExecuteNonQueryAsync();
    }
}

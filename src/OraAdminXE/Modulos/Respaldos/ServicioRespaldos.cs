using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Modulos.Respaldos;

/// <summary>Modos de Data Pump; el nombre coincide con TIPO_RESPALDO.CODIGO y con el job_mode de DBMS_DATAPUMP.</summary>
public enum TipoRespaldo { Full, Schema, Table }

/// <summary>Fila activa de ADMIN_BD.DIRECTORIO.</summary>
public sealed record DirectorioRespaldo(int Id, string NombreOracle, string RutaSo)
{
    public override string ToString() => $"{NombreOracle}  ·  {RutaSo}";
}

/// <summary>
/// Lo que el usuario eligió en el asistente. En modo <see cref="TipoRespaldo.Table"/> va un solo esquema
/// en <see cref="Esquemas"/> y sus tablas en <see cref="Tablas"/>; en <see cref="TipoRespaldo.Full"/> no van objetos.
/// </summary>
public sealed record SolicitudRespaldo(
    TipoRespaldo Tipo,
    DirectorioRespaldo Directorio,
    string Nombre,
    IReadOnlyList<string> Esquemas,
    IReadOnlyList<string> Tablas,
    string? Observacion = null)
{
    public string Codigo => Tipo.ToString().ToUpperInvariant();
    public string Job => Identificador.Validar(Nombre, "nombre del respaldo");
    public string ArchivoDump => Job + ".DMP";
    public string ArchivoLog => Job + ".LOG";
}

/// <summary>Cómo terminó un respaldo.</summary>
public sealed record ResultadoRespaldo(int IdRespaldo, string EstadoJob, long TamanoBytes)
{
    public bool Completado => EstadoJob == "COMPLETED";
    public double TamanoMb => TamanoBytes / 1048576.0;
}

/// <summary>
/// Respaldos con DBMS_DATAPUMP (issue #11): arma el bloque PL/SQL y ejecuta el flujo completo
/// (catálogo RESPALDO / RESPALDO_OBJETO → export → tamaño del .DMP → estado final → bitácora).
/// Lo reutilizan el catálogo (#12) y los respaldos programados (#30).
/// </summary>
public sealed class ServicioRespaldos(IBaseDatos bd)
{
    /// <summary>Nombre sugerido para el job y los archivos: EXP_{TIPO}_{yyyyMMdd_HHmmss}.</summary>
    public static string NombreSugerido(TipoRespaldo tipo, DateTime fecha) =>
        $"EXP_{tipo.ToString().ToUpperInvariant()}_{fecha:yyyyMMdd_HHmmss}";

    /// <summary>Valida la solicitud (lanza <see cref="ArgumentException"/> con un mensaje para el usuario).</summary>
    public static void Validar(SolicitudRespaldo s)
    {
        _ = s.Job;
        if (s.Tipo == TipoRespaldo.Schema && s.Esquemas.Count == 0)
            throw new ArgumentException("Marque al menos un esquema para respaldar.");
        if (s.Tipo == TipoRespaldo.Table && s.Esquemas.Count != 1)
            throw new ArgumentException("Elija el esquema de las tablas.");
        if (s.Tipo == TipoRespaldo.Table && s.Tablas.Count == 0)
            throw new ArgumentException("Marque al menos una tabla para respaldar.");
        foreach (var e in s.Esquemas) Identificador.Validar(e, "esquema");
        foreach (var t in s.Tablas) Identificador.Validar(t, "tabla");
    }

    /// <summary>El bloque PL/SQL exacto que se ejecuta (es el mismo que muestra «Ver SQL»).</summary>
    public static string ArmarBloque(SolicitudRespaldo s)
    {
        Validar(s);
        var filtros = new List<string>();
        if (s.Tipo != TipoRespaldo.Full) filtros.Add(BloqueDataPump.FiltroEsquemas(s.Esquemas));
        if (s.Tipo == TipoRespaldo.Table) filtros.Add(BloqueDataPump.FiltroTablas(s.Tablas));
        var directorio = Identificador.Validar(s.Directorio.NombreOracle, "directorio");

        return BloqueDataPump.Armar("EXPORT", s.Codigo, s.Job, directorio, s.ArchivoDump, s.ArchivoLog, filtros,
            declaraciones: ["existe BOOLEAN;", "tamano NUMBER;", "bloque NUMBER;"],
            alTerminar:
            [
                "-- Tamaño del .DMP generado",
                $"UTL_FILE.FGETATTR({Identificador.Literal(directorio)}, {Identificador.Literal(s.ArchivoDump)}, existe, tamano, bloque);",
                ":tamano := tamano;"
            ]);
    }

    /// <summary>Objetos que se guardan en RESPALDO_OBJETO: (esquema, NULL) por esquema o (esquema, tabla) por tabla.</summary>
    public static IReadOnlyList<(string Esquema, string? Tabla)> Objetos(SolicitudRespaldo s) => s.Tipo switch
    {
        TipoRespaldo.Schema => s.Esquemas.Select(e => (Identificador.Validar(e), (string?)null)).ToList(),
        TipoRespaldo.Table => s.Tablas.Select(t => (Identificador.Validar(s.Esquemas[0]), (string?)Identificador.Validar(t))).ToList(),
        _ => []
    };

    /// <summary>Revisa que el nombre no esté usado ni exista ya el .DMP en la carpeta (Data Pump no lo sobrescribe).</summary>
    public async Task ValidarDisponibleAsync(SolicitudRespaldo s)
    {
        Validar(s);
        if (Convert.ToInt32(await bd.EscalarAsync(SqlRespaldos.NombreOcupado, Parametro.De("n", s.Job))) > 0)
            throw new ArgumentException($"Ya existe un respaldo o un job de Data Pump llamado {s.Job}. Cambie el nombre.");
        if (File.Exists(Path.Combine(s.Directorio.RutaSo, s.ArchivoDump)))
            throw new ArgumentException($"Ya existe el archivo {s.ArchivoDump} en {s.Directorio.RutaSo}. Cambie el nombre.");
    }

    /// <summary>
    /// Ejecuta el respaldo de principio a fin en una sola sesión (otra sesión del pool puede consultar el progreso).
    /// Si Oracle falla, el respaldo queda en ERROR con el ORA en la observación, se registra en la bitácora y se relanza.
    /// </summary>
    public Task<ResultadoRespaldo> EjecutarAsync(SolicitudRespaldo s)
    {
        var bloque = ArmarBloque(s);
        var accion = $"Respaldo {s.Codigo}";
        // Task.Run: WAIT_FOR_JOB puede tardar minutos (full) y la pantalla no se debe congelar.
        return Task.Run(() => bd.EnMismaSesionAsync(async conexion =>
        {
            var id = await RegistrarAsync(conexion, s);
            try
            {
                var estado = new OracleParameter("estado", OracleDbType.Varchar2, 30) { Direction = ParameterDirection.Output };
                var tamano = new OracleParameter("tamano", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                await EjecutarAsync(conexion, bloque, estado, tamano);

                var resultado = new ResultadoRespaldo(id, estado.Value?.ToString() ?? "", Bytes(tamano.Value));
                await EjecutarAsync(conexion, SqlRespaldos.Terminar,
                    Parametro.De("estado", resultado.Completado ? "COMPLETADO" : "ERROR"),
                    Parametro.De("tamano", resultado.TamanoBytes), Parametro.De("id", id));
                if (!resultado.Completado)
                    await EjecutarAsync(conexion, SqlRespaldos.MarcarError,
                        Parametro.De("mensaje", $"El job terminó en estado {resultado.EstadoJob}"), Parametro.De("id", id));

                await bd.RegistrarBitacoraAsync(Modulo.Respaldos, accion, bloque, resultado.Completado,
                    $"{s.ArchivoDump}: {resultado.EstadoJob}, {resultado.TamanoMb:0.##} MB");
                return resultado;
            }
            catch (Exception ex)
            {
                try { await EjecutarAsync(conexion, SqlRespaldos.MarcarError, Parametro.De("mensaje", ex.Message), Parametro.De("id", id)); }
                catch (OracleException) { /* el error original es el que importa */ }
                await bd.RegistrarBitacoraAsync(Modulo.Respaldos, accion, bloque, false, ex.Message);
                throw;
            }
        }));
    }

    private static async Task<int> RegistrarAsync(OracleConnection conexion, SolicitudRespaldo s)
    {
        var id = new OracleParameter("id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
        await EjecutarAsync(conexion, SqlRespaldos.InsertarRespaldo,
            Parametro.De("tipo", s.Codigo), Parametro.De("dir", s.Directorio.Id), Parametro.De("job", s.Job),
            Parametro.De("dmp", s.ArchivoDump), Parametro.De("log", s.ArchivoLog),
            Parametro.De("obs", string.IsNullOrWhiteSpace(s.Observacion) ? null : s.Observacion.Trim()), id);
        var idRespaldo = Convert.ToInt32(id.Value.ToString());
        foreach (var (esquema, tabla) in Objetos(s))
            await EjecutarAsync(conexion, SqlRespaldos.InsertarObjeto,
                Parametro.De("id", idRespaldo), Parametro.De("esquema", esquema), Parametro.De("tabla", tabla));
        return idRespaldo;
    }

    private static async Task EjecutarAsync(OracleConnection conexion, string sql, params OracleParameter[] parametros)
    {
        await using var cmd = new OracleCommand(sql, conexion) { BindByName = true, CommandTimeout = 0 };
        cmd.Parameters.AddRange(parametros);
        await cmd.ExecuteNonQueryAsync();
    }

    private static long Bytes(object? valor) =>
        valor is Oracle.ManagedDataAccess.Types.OracleDecimal d && !d.IsNull ? (long)d.Value : 0;
}

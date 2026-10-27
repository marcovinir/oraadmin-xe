using System.Data;
using System.Diagnostics;
using Oracle.ManagedDataAccess.Client;

namespace OraAdminXE.Datos;

/// <summary>
/// Implementación de <see cref="IBaseDatos"/> con Oracle.ManagedDataAccess.Core.
/// Cada llamada toma una conexión del pool de ODP.NET y la devuelve al terminar,
/// así varias pantallas pueden consultar a la vez (por ejemplo, el progreso de un respaldo).
/// </summary>
public sealed class BaseDatos : IBaseDatos
{
    private const int MaxMensaje = 4000;
    private string _cadena = "";

    public ConfigConexion Config { get; private set; } = new();
    public string Usuario { get; private set; } = "";

    public async Task ConectarAsync(ConfigConexion config, string clave)
    {
        var cadena = new OracleConnectionStringBuilder
        {
            UserID = config.Usuario,
            Password = clave,
            DataSource = config.DataSource,
            Pooling = true
        }.ConnectionString;

        await using var conexion = new OracleConnection(cadena);
        await conexion.OpenAsync();

        await using var cmd = new OracleCommand(
            "SELECT SYS_CONTEXT('USERENV','SESSION_USER'), (SELECT COUNT(*) FROM session_roles WHERE role = 'DBA') FROM dual",
            conexion);
        await using var lector = await cmd.ExecuteReaderAsync();
        await lector.ReadAsync();
        var usuario = lector.GetString(0);
        if (lector.GetDecimal(1) == 0)
            throw new InvalidOperationException($"El usuario {usuario} no tiene el rol DBA. Conéctese con un usuario administrador (por ejemplo ADMIN_BD).");

        _cadena = cadena;
        Config = config;
        Usuario = usuario;
    }

    public async Task<DataTable> ConsultarAsync(string sql, params OracleParameter[] parametros)
    {
        await using var conexion = await AbrirAsync();
        await using var cmd = Comando(conexion, sql, parametros);
        await using var lector = await cmd.ExecuteReaderAsync();
        var tabla = new DataTable();
        tabla.Load(lector);
        return tabla;
    }

    public async Task<object?> EscalarAsync(string sql, params OracleParameter[] parametros)
    {
        await using var conexion = await AbrirAsync();
        await using var cmd = Comando(conexion, sql, parametros);
        var valor = await cmd.ExecuteScalarAsync();
        return valor is DBNull ? null : valor;
    }

    public async Task<int> EjecutarAsync(Modulo modulo, string accion, string sql, params OracleParameter[] parametros)
    {
        try
        {
            int filas;
            await using (var conexion = await AbrirAsync())
            await using (var cmd = Comando(conexion, sql, parametros))
                filas = await cmd.ExecuteNonQueryAsync();

            await RegistrarBitacoraAsync(modulo, accion, sql, true, null);
            return filas;
        }
        catch (Exception ex)
        {
            await RegistrarBitacoraAsync(modulo, accion, sql, false, ex.Message);
            throw;
        }
    }

    public async Task<T> EnMismaSesionAsync<T>(Func<OracleConnection, Task<T>> trabajo)
    {
        await using var conexion = await AbrirAsync();
        return await trabajo(conexion);
    }

    public async Task RegistrarBitacoraAsync(Modulo modulo, string accion, string? sentencia, bool ok, string? mensaje)
    {
        const string sql = """
            INSERT INTO admin_bd.bitacora (id_modulo, usuario_bd, accion, sentencia, resultado, mensaje)
            VALUES ((SELECT id_modulo FROM admin_bd.modulo WHERE nombre = :modulo), :usuario, :accion, :sentencia, :resultado, :mensaje)
            """;
        try
        {
            await using var conexion = await AbrirAsync();
            await using var cmd = Comando(conexion, sql,
                new OracleParameter("modulo", modulo.ToString().ToUpperInvariant()),
                new OracleParameter("usuario", Usuario),
                new OracleParameter("accion", Recortar(accion, 100)),
                new OracleParameter("sentencia", OracleDbType.Clob) { Value = sentencia is null ? DBNull.Value : Sentencias.OcultarClaves(sentencia) },
                new OracleParameter("resultado", ok ? "OK" : "ERROR"),
                new OracleParameter("mensaje", (object?)Recortar(mensaje, MaxMensaje) ?? DBNull.Value));
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // La bitácora nunca debe romper la acción del usuario.
            Debug.WriteLine($"No se pudo registrar en la bitácora: {ex.Message}");
        }
    }

    private async Task<OracleConnection> AbrirAsync()
    {
        if (_cadena.Length == 0)
            throw new InvalidOperationException("No hay una conexión activa. Inicie sesión primero.");
        var conexion = new OracleConnection(_cadena);
        await conexion.OpenAsync();
        return conexion;
    }

    private static OracleCommand Comando(OracleConnection conexion, string sql, params OracleParameter[] parametros)
    {
        var cmd = new OracleCommand(Sentencias.Normalizar(sql), conexion) { BindByName = true, CommandTimeout = 0 };
        cmd.Parameters.AddRange(parametros);
        return cmd;
    }

    private static string? Recortar(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];
}

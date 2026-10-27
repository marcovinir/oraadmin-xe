using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace OraAdminXE.Datos;

/// <summary>
/// Contrato único de acceso a Oracle. TODA llamada a la base de datos pasa por aquí,
/// así la bitácora, los errores ORA y la conexión se manejan igual en todos los módulos.
/// La implementación vive en <see cref="BaseDatos"/>.
/// </summary>
public interface IBaseDatos
{
    /// <summary>Datos de la conexión activa (sin contraseña).</summary>
    ConfigConexion Config { get; }

    /// <summary>Usuario de Oracle conectado, en mayúsculas (ej. ADMIN_BD).</summary>
    string Usuario { get; }

    /// <summary>
    /// Abre la conexión y valida que el usuario tenga el rol DBA.
    /// Lanza <see cref="OracleException"/> si las credenciales fallan o
    /// <see cref="InvalidOperationException"/> si el usuario no es DBA.
    /// </summary>
    Task ConectarAsync(ConfigConexion config, string clave);

    /// <summary>SELECT con variables bind. No se registra en la bitácora.</summary>
    Task<DataTable> ConsultarAsync(string sql, params OracleParameter[] parametros);

    /// <summary>SELECT que devuelve un solo valor (o null).</summary>
    Task<object?> EscalarAsync(string sql, params OracleParameter[] parametros);

    /// <summary>
    /// DDL, DML o bloque PL/SQL. Registra SIEMPRE en la bitácora (OK o ERROR con el ORA completo)
    /// y relanza la excepción si falla. Devuelve las filas afectadas.
    /// </summary>
    Task<int> EjecutarAsync(Modulo modulo, string accion, string sql, params OracleParameter[] parametros);

    /// <summary>
    /// Ejecuta varias sentencias sobre la MISMA sesión de Oracle (por ejemplo, ejecutar una consulta
    /// con GATHER_PLAN_STATISTICS y luego leer DBMS_XPLAN.DISPLAY_CURSOR).
    /// </summary>
    Task<T> EnMismaSesionAsync<T>(Func<OracleConnection, Task<T>> trabajo);

    /// <summary>Escribe una fila en BITACORA. Lo usan EjecutarAsync y las acciones largas (respaldos).</summary>
    Task RegistrarBitacoraAsync(Modulo modulo, string accion, string? sentencia, bool ok, string? mensaje);
}

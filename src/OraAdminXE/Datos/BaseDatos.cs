using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace OraAdminXE.Datos;

/// <summary>
/// Implementación de <see cref="IBaseDatos"/> con Oracle.ManagedDataAccess.Core.
/// PENDIENTE: issue «Núcleo: capa de acceso a datos y bitácora».
/// </summary>
public sealed class BaseDatos : IBaseDatos
{
    public ConfigConexion Config { get; private set; } = new();
    public string Usuario { get; private set; } = "";

    public Task ConectarAsync(ConfigConexion config, string clave) =>
        throw new NotImplementedException("Issue Núcleo: capa de acceso a datos");

    public Task<DataTable> ConsultarAsync(string sql, params OracleParameter[] parametros) =>
        throw new NotImplementedException("Issue Núcleo: capa de acceso a datos");

    public Task<object?> EscalarAsync(string sql, params OracleParameter[] parametros) =>
        throw new NotImplementedException("Issue Núcleo: capa de acceso a datos");

    public Task<int> EjecutarAsync(Modulo modulo, string accion, string sql, params OracleParameter[] parametros) =>
        throw new NotImplementedException("Issue Núcleo: capa de acceso a datos");

    public Task<T> EnMismaSesionAsync<T>(Func<OracleConnection, Task<T>> trabajo) =>
        throw new NotImplementedException("Issue Núcleo: capa de acceso a datos");

    public Task RegistrarBitacoraAsync(Modulo modulo, string accion, string? sentencia, bool ok, string? mensaje) =>
        throw new NotImplementedException("Issue Núcleo: capa de acceso a datos");
}

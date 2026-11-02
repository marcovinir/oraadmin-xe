using Oracle.ManagedDataAccess.Client;

namespace OraAdminXE.Datos;

/// <summary>
/// Crea variables bind sin la trampa de ODP.NET: <c>new OracleParameter("x", 0)</c> NO pasa el valor 0,
/// porque C# convierte el literal 0 a <see cref="OracleDbType"/> y usa el constructor del tipo
/// (falla con ArgumentOutOfRangeException). Usar siempre <c>Parametro.De("x", 0)</c>.
/// </summary>
public static class Parametro
{
    public static OracleParameter De(string nombre, object? valor) => new(nombre, valor ?? DBNull.Value);
}

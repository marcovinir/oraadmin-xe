using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;

namespace OraAdminXE.Tests;

/// <summary>
/// Limpieza de las pruebas de integración sin dejar rastro en la bitácora real (la que se muestra en la demo).
/// </summary>
public static class LimpiezaOracle
{
    /// <summary>Ejecuta una sentencia sin registrarla en la bitácora; si falla (ya no existe), se ignora.</summary>
    public static Task SinBitacoraAsync(BaseDatos bd, string sql, params OracleParameter[] parametros) =>
        bd.EnMismaSesionAsync(async conexion =>
        {
            try
            {
                await using var cmd = new OracleCommand(sql, conexion) { BindByName = true };
                cmd.Parameters.AddRange(parametros);
                return await cmd.ExecuteNonQueryAsync();
            }
            catch (OracleException) { return 0; }
        });

    /// <summary>Borra de la bitácora todo lo que la prueba registró (las filas cuyo SQL o mensaje mencionan la marca).</summary>
    public static Task BorrarBitacoraAsync(BaseDatos bd, string marca) =>
        SinBitacoraAsync(bd,
            "DELETE FROM admin_bd.bitacora WHERE INSTR(mensaje, :m) > 0 OR DBMS_LOB.INSTR(sentencia, :m) > 0",
            Parametro.De("m", marca));
}

using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Bitacora;

namespace OraAdminXE.Tests;

/// <summary>Filtros de la Bitácora contra Oracle (issue #7).</summary>
public class BitacoraTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _accion = "Prueba bitácora " + Guid.NewGuid().ToString("N")[..8];

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave))) return;
        await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
        await _bd.RegistrarBitacoraAsync(Modulo.Respaldos, _accion, "SELECT 1 FROM dual", true, null);
        await _bd.RegistrarBitacoraAsync(Modulo.Respaldos, _accion, "SELECT x FROM dual", false, "ORA-00904: \"X\": identificador no válido");
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        // Se borra sin pasar por EjecutarAsync, para no dejar en la bitácora la propia limpieza.
        await _bd.EnMismaSesionAsync(async conexion =>
        {
            await using var cmd = new Oracle.ManagedDataAccess.Client.OracleCommand("DELETE FROM admin_bd.bitacora WHERE accion = :a", conexion);
            cmd.Parameters.Add(Parametro.De("a", _accion));
            return await cmd.ExecuteNonQueryAsync();
        });
    }

    private Task<DataTable> Buscar(string? modulo, string? resultado, string? texto) =>
        _bd.ConsultarAsync(SqlBitacora.Buscar,
            Parametro.De("desde", DateTime.Today), Parametro.De("hasta", DateTime.Today),
            Parametro.De("modulo", modulo), Parametro.De("resultado", resultado), Parametro.De("texto", texto));

    [OracleFact]
    public async Task Filtra_por_modulo_resultado_y_texto()
    {
        var ambas = await Buscar("RESPALDOS", null, _accion.ToUpperInvariant());
        Assert.Equal(2, ambas.Rows.Count);
        Assert.Equal("SELECT x FROM dual", ambas.Rows[0]["SENTENCIA"]);   // la más reciente primero

        var errores = await Buscar(null, "ERROR", _accion);
        Assert.Equal("ERROR", Assert.Single(errores.AsEnumerable())["RESULTADO"]);

        Assert.Empty((await Buscar("TUNING", null, _accion)).Rows);
        Assert.Contains("RESPALDOS", (await _bd.ConsultarAsync(SqlBitacora.Modulos)).AsEnumerable().Select(r => (string)r[0]));
    }

    [OracleFact]
    public async Task Sin_filtros_trae_lo_de_hoy_con_un_maximo()
    {
        var hoy = await Buscar(null, null, null);
        Assert.InRange(hoy.Rows.Count, 2, SqlBitacora.Maximo);
    }
}

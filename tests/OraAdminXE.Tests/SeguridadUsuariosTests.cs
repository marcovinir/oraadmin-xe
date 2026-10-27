using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Seguridad;

namespace OraAdminXE.Tests;

/// <summary>Ciclo completo de un usuario contra Oracle con el DDL de la pantalla (issue #8).</summary>
public class SeguridadUsuariosTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _usuario = "UPRUEBA_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        try { await _bd.EjecutarAsync(Modulo.Seguridad, "Limpieza de prueba", DdlUsuarios.Eliminar(_usuario)); }
        catch (OracleException) { }
    }

    private async Task<string> Estado() =>
        (string)(await _bd.EscalarAsync(SqlSeguridad.EstadoUsuario, new OracleParameter("u", _usuario)))!;

    [OracleFact]
    public async Task Crear_bloquear_desbloquear_expirar_y_eliminar()
    {
        foreach (var sql in DdlUsuarios.Crear(_usuario, "Clave_2026", "USERS", 10, "DEFAULT", otorgarSesion: true))
            await _bd.EjecutarAsync(Modulo.Seguridad, "Crear usuario", sql);
        Assert.Equal("OPEN", await Estado());

        var lista = await _bd.ConsultarAsync(SqlSeguridad.Usuarios, Parametro.De("todos", 0));
        var fila = lista.Select($"USERNAME = '{_usuario}'").Single();
        Assert.Equal("10", fila["CUOTA_MB"]);

        await _bd.EjecutarAsync(Modulo.Seguridad, "Bloquear", DdlUsuarios.Bloquear(_usuario));
        Assert.Equal("LOCKED", await Estado());

        await _bd.EjecutarAsync(Modulo.Seguridad, "Desbloquear", DdlUsuarios.Desbloquear(_usuario));
        Assert.Equal("OPEN", await Estado());

        await _bd.EjecutarAsync(Modulo.Seguridad, "Expirar", DdlUsuarios.Expirar(_usuario));
        Assert.Equal("EXPIRED", await Estado());

        await _bd.EjecutarAsync(Modulo.Seguridad, "Cambiar clave", DdlUsuarios.CambiarClave(_usuario, "Otra_2026"));
        Assert.Equal("OPEN", await Estado());

        await _bd.EjecutarAsync(Modulo.Seguridad, "Cuota", DdlUsuarios.CambiarTablespace(_usuario, "USERS", null));
        lista = await _bd.ConsultarAsync(SqlSeguridad.Usuarios, Parametro.De("todos", 0));
        Assert.Equal("ILIMITADA", lista.Select($"USERNAME = '{_usuario}'").Single()["CUOTA_MB"]);

        await _bd.EjecutarAsync(Modulo.Seguridad, "Eliminar", DdlUsuarios.Eliminar(_usuario));
        Assert.Null(await _bd.EscalarAsync(SqlSeguridad.EstadoUsuario, new OracleParameter("u", _usuario)));
    }

    [OracleFact]
    public async Task Listas_de_apoyo_tienen_datos()
    {
        Assert.Contains("USERS", (await _bd.ConsultarAsync(SqlSeguridad.TablespacesPermanentes)).AsEnumerable().Select(r => (string)r[0]));
        Assert.Contains("DEFAULT", (await _bd.ConsultarAsync(SqlSeguridad.Perfiles)).AsEnumerable().Select(r => (string)r[0]));
        var todos = await _bd.ConsultarAsync(SqlSeguridad.Usuarios, Parametro.De("todos", 1));
        Assert.Contains(todos.AsEnumerable(), r => (string)r["USERNAME"] == "SYS");
    }
}

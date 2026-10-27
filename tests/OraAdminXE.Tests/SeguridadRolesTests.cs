using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Seguridad;

namespace OraAdminXE.Tests;

/// <summary>Roles y privilegios contra Oracle con el DDL de la pantalla (issue #9).</summary>
public class SeguridadRolesTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    private string Rol => "RPRUEBA_" + _sufijo;
    private string Usuario => "UPRUEBA_" + _sufijo;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        foreach (var sql in new[] { DdlUsuarios.Eliminar(Usuario), DdlRoles.EliminarRol(Rol) })
            try { await _bd.EjecutarAsync(Modulo.Seguridad, "Limpieza de prueba", sql); } catch (OracleException) { }
    }

    private async Task<DataRow[]> Privilegios(string g) =>
        (await _bd.ConsultarAsync(SqlSeguridad.PrivilegiosDe, Parametro.De("g", g))).Select();

    [OracleFact]
    public async Task Crear_rol_otorgar_y_revocar()
    {
        await _bd.EjecutarAsync(Modulo.Seguridad, "Crear rol", DdlRoles.CrearRol(Rol));
        await _bd.EjecutarAsync(Modulo.Seguridad, "Otorgar", DdlRoles.OtorgarSistema("CREATE SESSION", Rol, false));
        await _bd.EjecutarAsync(Modulo.Seguridad, "Otorgar", DdlRoles.OtorgarObjeto(["SELECT", "INSERT"], "DEMO_VENTAS", "FACTURA", Rol, false));
        foreach (var sql in DdlUsuarios.Crear(Usuario, "Clave_2026", "USERS", 5, "DEFAULT", otorgarSesion: false))
            await _bd.EjecutarAsync(Modulo.Seguridad, "Crear usuario", sql);
        await _bd.EjecutarAsync(Modulo.Seguridad, "Otorgar", DdlRoles.OtorgarRol(Rol, Usuario, false));

        var delRol = await Privilegios(Rol);
        Assert.Contains(delRol, r => (string)r["TIPO"] == "SISTEMA" && (string)r["PRIVILEGIO"] == "CREATE SESSION");
        Assert.Contains(delRol, r => (string)r["TIPO"] == "OBJETO" && (string)r["PRIVILEGIO"] == "SELECT" && (string)r["OBJETO"] == "DEMO_VENTAS.FACTURA");
        var delUsuario = await Privilegios(Usuario);
        Assert.Contains(delUsuario, r => (string)r["TIPO"] == "ROL" && (string)r["PRIVILEGIO"] == Rol);

        var roles = await _bd.ConsultarAsync(SqlSeguridad.Roles, Parametro.De("todos", 0));
        // Oracle otorga el rol a quien lo crea (ADMIN_BD, con ADMIN OPTION) además del usuario de prueba.
        Assert.Equal(2m, Convert.ToDecimal(roles.Select($"ROLE = '{Rol}'").Single()["OTORGADO_A"]));
        Assert.Contains(await Privilegios("ADMIN_BD"), r => (string)r["PRIVILEGIO"] == Rol && (string)r["CON_OPCION"] == "YES");

        await _bd.EjecutarAsync(Modulo.Seguridad, "Revocar", DdlRoles.Revocar("OBJETO", "INSERT", "DEMO_VENTAS.FACTURA", Rol));
        await _bd.EjecutarAsync(Modulo.Seguridad, "Revocar", DdlRoles.Revocar("ROL", Rol, null, Usuario));
        Assert.DoesNotContain(await Privilegios(Rol), r => (string)r["PRIVILEGIO"] == "INSERT");
        Assert.Empty(await Privilegios(Usuario));
    }

    [OracleFact]
    public async Task Listas_de_apoyo()
    {
        var beneficiarios = await _bd.ConsultarAsync(SqlSeguridad.Beneficiarios);
        Assert.Contains(beneficiarios.Select(), r => (string)r["NOMBRE"] == "DEMO_VENTAS" && (string)r["TIPO"] == "USUARIO");
        Assert.Contains((await _bd.ConsultarAsync(SqlSeguridad.TodosLosRoles)).Select(), r => (string)r[0] == "CONNECT");
        Assert.Contains((await _bd.ConsultarAsync(SqlSeguridad.PrivilegiosSistema)).Select(), r => (string)r[0] == "CREATE SESSION");
        Assert.Contains((await _bd.ConsultarAsync(SqlSeguridad.TablasDe, Parametro.De("e", "DEMO_VENTAS"))).Select(), r => (string)r[0] == "FACTURA");
    }
}

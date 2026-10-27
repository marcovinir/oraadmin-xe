using OraAdminXE.Modulos.Seguridad;

namespace OraAdminXE.Tests;

public class DdlRolesTests
{
    [Fact]
    public void Crear_y_eliminar_rol()
    {
        Assert.Equal("CREATE ROLE ROL_CONSULTA", DdlRoles.CrearRol("rol_consulta"));
        Assert.Equal("DROP ROLE ROL_CONSULTA", DdlRoles.EliminarRol("rol_consulta"));
    }

    [Fact]
    public void Otorgar_rol_y_privilegio_de_sistema()
    {
        Assert.Equal("GRANT ROL_CONSULTA TO JUAN", DdlRoles.OtorgarRol("rol_consulta", "juan", false));
        Assert.Equal("GRANT CONNECT TO JUAN WITH ADMIN OPTION", DdlRoles.OtorgarRol("connect", "juan", true));
        Assert.Equal("GRANT CREATE SESSION TO ROL_CONSULTA", DdlRoles.OtorgarSistema("create session", "rol_consulta", false));
        Assert.Equal("GRANT UNLIMITED TABLESPACE TO JUAN WITH ADMIN OPTION", DdlRoles.OtorgarSistema("UNLIMITED TABLESPACE", "JUAN", true));
    }

    [Fact]
    public void Otorgar_privilegios_de_objeto()
    {
        Assert.Equal("GRANT SELECT, INSERT ON DEMO_VENTAS.FACTURA TO ROL_CONSULTA",
            DdlRoles.OtorgarObjeto(["select", "insert"], "demo_ventas", "factura", "rol_consulta", false));
        Assert.Equal("GRANT SELECT ON DEMO_VENTAS.FACTURA TO JUAN WITH GRANT OPTION",
            DdlRoles.OtorgarObjeto(["SELECT"], "DEMO_VENTAS", "FACTURA", "JUAN", true));
    }

    [Fact]
    public void Otorgar_objeto_rechaza_privilegios_desconocidos_o_vacios()
    {
        Assert.Throws<ArgumentException>(() => DdlRoles.OtorgarObjeto([], "DEMO_VENTAS", "FACTURA", "JUAN", false));
        Assert.Throws<ArgumentException>(() => DdlRoles.OtorgarObjeto(["DROP"], "DEMO_VENTAS", "FACTURA", "JUAN", false));
    }

    [Theory]
    [InlineData("CREATE SESSION; DROP USER SYS")]
    [InlineData("")]
    [InlineData("create-session")]
    public void Otorgar_sistema_rechaza_nombres_raros(string privilegio) =>
        Assert.Throws<ArgumentException>(() => DdlRoles.OtorgarSistema(privilegio, "JUAN", false));

    [Fact]
    public void Revocar_segun_el_tipo()
    {
        Assert.Equal("REVOKE ROL_CONSULTA FROM JUAN", DdlRoles.Revocar("ROL", "ROL_CONSULTA", null, "JUAN"));
        Assert.Equal("REVOKE CREATE SESSION FROM JUAN", DdlRoles.Revocar("SISTEMA", "CREATE SESSION", null, "JUAN"));
        Assert.Equal("REVOKE SELECT ON DEMO_VENTAS.FACTURA FROM JUAN", DdlRoles.Revocar("OBJETO", "SELECT", "DEMO_VENTAS.FACTURA", "JUAN"));
    }
}

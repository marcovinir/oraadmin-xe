using OraAdminXE.Modulos.Seguridad;

namespace OraAdminXE.Tests;

public class DdlUsuariosTests
{
    [Fact]
    public void Crear_con_cuota_perfil_y_create_session()
    {
        var sql = DdlUsuarios.Crear("juan", "Clave_2026", "users", 50, "perfil_demo", otorgarSesion: true);
        Assert.Equal(
        [
            "CREATE USER JUAN IDENTIFIED BY \"Clave_2026\" DEFAULT TABLESPACE USERS TEMPORARY TABLESPACE TEMP QUOTA 50M ON USERS PROFILE PERFIL_DEMO",
            "GRANT CREATE SESSION TO JUAN"
        ], sql);
    }

    [Fact]
    public void Crear_con_cuota_ilimitada_y_sin_sesion()
    {
        var sql = DdlUsuarios.Crear("ana", "x1", "USERS", null, "DEFAULT", otorgarSesion: false);
        Assert.Equal(["CREATE USER ANA IDENTIFIED BY \"x1\" DEFAULT TABLESPACE USERS TEMPORARY TABLESPACE TEMP QUOTA UNLIMITED ON USERS PROFILE DEFAULT"], sql);
    }

    [Theory]
    [InlineData("")]
    [InlineData("con\"comilla")]
    public void Crear_rechaza_claves_invalidas(string clave) =>
        Assert.Throws<ArgumentException>(() => DdlUsuarios.Crear("juan", clave, "USERS", 10, "DEFAULT", true));

    [Fact]
    public void Crear_rechaza_nombres_peligrosos() =>
        Assert.Throws<ArgumentException>(() => DdlUsuarios.Crear("juan; drop user sys", "x", "USERS", 10, "DEFAULT", true));

    [Fact]
    public void Acciones_simples()
    {
        Assert.Equal("ALTER USER JUAN ACCOUNT LOCK", DdlUsuarios.Bloquear("juan"));
        Assert.Equal("ALTER USER JUAN ACCOUNT UNLOCK", DdlUsuarios.Desbloquear("juan"));
        Assert.Equal("ALTER USER JUAN PASSWORD EXPIRE", DdlUsuarios.Expirar("juan"));
        Assert.Equal("ALTER USER JUAN IDENTIFIED BY \"Nueva_1\"", DdlUsuarios.CambiarClave("juan", "Nueva_1"));
        Assert.Equal("DROP USER JUAN CASCADE", DdlUsuarios.Eliminar("juan"));
        Assert.Equal("ALTER USER JUAN DEFAULT TABLESPACE TS_VENTAS QUOTA 20M ON TS_VENTAS", DdlUsuarios.CambiarTablespace("juan", "ts_ventas", 20));
    }

    [Theory]
    [InlineData("SYS", "N", "ADMIN_BD", true)]
    [InlineData("SYSTEM", "N", "ADMIN_BD", true)]
    [InlineData("XDB", "Y", "ADMIN_BD", true)]
    [InlineData("ADMIN_BD", "N", "ADMIN_BD", true)]
    [InlineData("JUAN", "N", "ADMIN_BD", false)]
    public void EsProtegido(string usuario, string oracleMaintained, string sesion, bool esperado) =>
        Assert.Equal(esperado, DdlUsuarios.EsProtegido(usuario, oracleMaintained, sesion));
}

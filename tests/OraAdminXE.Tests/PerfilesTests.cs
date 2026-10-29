using System.Data;
using Oracle.ManagedDataAccess.Client;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Seguridad;

namespace OraAdminXE.Tests;

/// <summary>DDL de perfiles (issue #10), sin base de datos.</summary>
public class DdlPerfilesTests
{
    [Fact]
    public void Crear_perfil_de_la_demo()
    {
        var sql = DdlPerfiles.Crear("perfil_demo",
        [
            new("FAILED_LOGIN_ATTEMPTS", ModoLimite.Valor, 3), new("PASSWORD_LOCK_TIME", ModoLimite.Valor, 1),
            new("PASSWORD_LIFE_TIME", ModoLimite.Valor, 90), new("SESSIONS_PER_USER", ModoLimite.Valor, 2), new("IDLE_TIME", ModoLimite.Valor, 15)
        ]);
        Assert.Equal("CREATE PROFILE PERFIL_DEMO LIMIT FAILED_LOGIN_ATTEMPTS 3 PASSWORD_LOCK_TIME 1/1440 PASSWORD_LIFE_TIME 90 SESSIONS_PER_USER 2 IDLE_TIME 15", sql);
        Assert.DoesNotContain("PASSWORD_VERIFY_FUNCTION", sql);
    }

    [Fact]
    public void Modificar_con_unlimited_y_default()
    {
        Assert.Equal("ALTER PROFILE PERFIL_DEMO LIMIT FAILED_LOGIN_ATTEMPTS 5 IDLE_TIME UNLIMITED CONNECT_TIME DEFAULT",
            DdlPerfiles.Modificar("PERFIL_DEMO",
                [new("FAILED_LOGIN_ATTEMPTS", ModoLimite.Valor, 5), new("IDLE_TIME", ModoLimite.Unlimited), new("CONNECT_TIME", ModoLimite.Default)]));
    }

    [Theory]
    [InlineData("DEFAULT")]
    [InlineData("default")]
    [InlineData("ORA_STIG_PROFILE")]
    public void DEFAULT_y_los_de_Oracle_no_se_borran_ni_se_modifican(string perfil)
    {
        Assert.True(DdlPerfiles.EsProtegido(perfil));
        Assert.Throws<ArgumentException>(() => DdlPerfiles.Eliminar(perfil));
        Assert.Throws<ArgumentException>(() => DdlPerfiles.Modificar(perfil, [new("IDLE_TIME", ModoLimite.Valor, 5)]));
    }

    [Fact]
    public void Eliminar_y_asignar()
    {
        Assert.Equal("DROP PROFILE PERFIL_DEMO CASCADE", DdlPerfiles.Eliminar("perfil_demo"));
        Assert.Equal("ALTER USER JUAN PROFILE PERFIL_DEMO", DdlPerfiles.Asignar("juan", "perfil_demo"));
    }

    [Fact]
    public void Validaciones()
    {
        Assert.Throws<ArgumentException>(() => DdlPerfiles.Crear("P", []));
        Assert.Throws<ArgumentException>(() => DdlPerfiles.Crear("P", [new("IDLE_TIME", ModoLimite.Valor, 0)]));
        Assert.Throws<ArgumentException>(() => DdlPerfiles.Crear("P; DROP USER SYS", [new("IDLE_TIME", ModoLimite.Valor, 5)]));
    }

    [Theory]
    [InlineData("PASSWORD_LOCK_TIME", ",0006", ModoLimite.Valor, 1)]    // 1/1440 con coma decimal (sesión en español)
    [InlineData("PASSWORD_LOCK_TIME", ".0208", ModoLimite.Valor, 30)]   // 30/1440
    [InlineData("FAILED_LOGIN_ATTEMPTS", "3", ModoLimite.Valor, 3)]
    [InlineData("IDLE_TIME", "UNLIMITED", ModoLimite.Unlimited, 0)]
    [InlineData("CONNECT_TIME", "DEFAULT", ModoLimite.Default, 0)]
    public void Leer_el_limite_de_DBA_PROFILES(string recurso, string limite, ModoLimite modo, int numero)
    {
        var l = DdlPerfiles.Leer(recurso, limite);
        Assert.Equal(modo, l.Modo);
        if (modo == ModoLimite.Valor) Assert.Equal(numero, l.Numero);
    }

    [Theory]
    [InlineData("PASSWORD_LOCK_TIME", ",0006", "1 min")]
    [InlineData("PASSWORD_LOCK_TIME", "1", "1 día")]
    [InlineData("PASSWORD_LOCK_TIME", "UNLIMITED", "UNLIMITED")]
    [InlineData("FAILED_LOGIN_ATTEMPTS", "10", "10")]
    public void Valor_legible_en_la_grilla(string recurso, string limite, string esperado) =>
        Assert.Equal(esperado, DdlPerfiles.ValorLegible(recurso, limite));
}

/// <summary>Criterio de aceptación de #10 contra Oracle: el perfil bloquea al usuario tras 3 intentos fallidos.</summary>
public class PerfilesIntegracionTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();
    private readonly string _sufijo = DateTime.Now.ToString("HHmmssfff");
    private string Perfil => "PERFIL_PRUEBA_" + _sufijo;
    private string Usuario => "UPERFIL_" + _sufijo;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_bd.Usuario)) return;
        try { await _bd.EjecutarAsync(Modulo.Seguridad, "Limpieza de prueba", DdlUsuarios.Eliminar(Usuario)); } catch (OracleException) { }
        try { await _bd.EjecutarAsync(Modulo.Seguridad, "Limpieza de prueba", DdlPerfiles.Eliminar(Perfil)); } catch (OracleException) { }
    }

    [OracleFact]
    public async Task Tres_intentos_fallidos_dejan_al_usuario_LOCKED_TIMED()
    {
        await _bd.EjecutarAsync(Modulo.Seguridad, "Crear perfil", DdlPerfiles.Crear(Perfil,
            [new("FAILED_LOGIN_ATTEMPTS", ModoLimite.Valor, 3), new("PASSWORD_LOCK_TIME", ModoLimite.Valor, 1)]));
        var limites = await _bd.ConsultarAsync(SqlPerfiles.LimitesDe, Parametro.De("p", Perfil));
        var lectura = limites.AsEnumerable().ToDictionary(r => (string)r["RESOURCE_NAME"], r => DdlPerfiles.Leer((string)r["RESOURCE_NAME"], (string)r["LIMIT"]));
        Assert.Equal(3, lectura["FAILED_LOGIN_ATTEMPTS"].Numero);
        Assert.Equal(1, lectura["PASSWORD_LOCK_TIME"].Numero);   // vuelve en minutos aunque Oracle lo guarde en días

        foreach (var sql in DdlUsuarios.Crear(Usuario, "Clave_2026", "USERS", 1, "DEFAULT", otorgarSesion: true))
            await _bd.EjecutarAsync(Modulo.Seguridad, "Crear usuario", sql);
        await _bd.EjecutarAsync(Modulo.Seguridad, "Asignar perfil", DdlPerfiles.Asignar(Usuario, Perfil));

        var cadena = new OracleConnectionStringBuilder { UserID = Usuario, Password = "Mala_clave", DataSource = new ConfigConexion().DataSource, Pooling = false };
        for (var i = 0; i < 3; i++)
        {
            await using var conexion = new OracleConnection(cadena.ConnectionString);
            var ex = await Assert.ThrowsAsync<OracleException>(() => conexion.OpenAsync());
            Assert.Equal(1017, ex.Number);
        }

        Assert.Equal("LOCKED(TIMED)", await _bd.EscalarAsync(SqlPerfiles.EstadoUsuario, Parametro.De("u", Usuario)));
    }
}

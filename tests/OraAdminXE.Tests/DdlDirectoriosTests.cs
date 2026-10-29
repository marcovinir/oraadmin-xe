using OraAdminXE.Modulos.Directorios;

namespace OraAdminXE.Tests;

/// <summary>Validaciones puras de Directorios: no abre Oracle ni crea carpetas.</summary>
public class DdlDirectoriosTests
{
    [Fact]
    public void Crear_valida_identificador_y_escapa_ruta()
    {
        var sql = DdlDirectorios.Crear("dir_respaldos2", @"C:\respaldos_bd2\o'brien");

        Assert.Equal(@"CREATE DIRECTORY DIR_RESPALDOS2 AS 'C:\respaldos_bd2\o''brien'", sql);
        Assert.DoesNotContain("OR REPLACE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Crear_con_ruta_windows_normal_conserva_una_sola_barra_invertida()
    {
        var sql = DdlDirectorios.Crear("dir_respaldos2", @"C:\respaldos_bd2\2026");

        Assert.Equal(@"CREATE DIRECTORY DIR_RESPALDOS2 AS 'C:\respaldos_bd2\2026'", sql);
        Assert.DoesNotContain(@"\\", sql);
    }

    [Theory]
    [InlineData("dir; DROP DIRECTORY DATA_PUMP_DIR")]
    [InlineData("1DIR")]
    public void Crear_rechaza_identificadores_peligrosos(string nombre) =>
        Assert.Throws<ArgumentException>(() => DdlDirectorios.Crear(nombre, @"C:\respaldos_bd2\prueba"));

    [Theory]
    [InlineData("respaldos")]
    [InlineData(@"\\servidor\recurso")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\respaldos\..\otro")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Oracle\product\21c")]
    [InlineData(@"C:\Users\operador\respaldos")]
    [InlineData(@"C:\respaldos_bd")]
    public void ValidarRutaCreacion_rechaza_rutas_no_locales_o_riesgosas(string ruta) =>
        Assert.Throws<ArgumentException>(() => DdlDirectorios.ValidarRutaCreacion(ruta));

    [Fact]
    public void ValidarRutaCreacion_normaliza_ruta_local_absoluta() =>
        Assert.Equal(@"C:\respaldos_bd2\prueba", DdlDirectorios.ValidarRutaCreacion(@"C:\respaldos_bd2\prueba\"));

    [Fact]
    public void EsNombreReservado_impide_crear_directorios_base_del_proyecto() =>
        Assert.True(DdlDirectorios.EsNombreReservado("DIR_RESPALDOS"));

    [Theory]
    [InlineData("DIR_RESPALDOS2", true)]
    [InlineData("DIR_RESPALDOS3", false)]
    [InlineData("DATA_PUMP_DIR", false)]
    public void EsNombreDemostracionPermitido_es_allowlist_explicita(string directorio, bool esperado) =>
        Assert.Equal(esperado, DdlDirectorios.EsNombreDemostracionPermitido(directorio));

    [Theory]
    [InlineData("SYS", "N", "ADMIN_BD", true)]
    [InlineData("SYSTEM", "N", "ADMIN_BD", true)]
    [InlineData("XDB", "Y", "ADMIN_BD", true)]
    [InlineData("ADMIN_BD", "N", "ADMIN_BD", true)]
    [InlineData("RESPALDOS_APP", "N", "ADMIN_BD", false)]
    public void EsUsuarioProtegido_rechaza_cuentas_internas_y_sesion(string usuario, string mantenido, string sesion, bool esperado) =>
        Assert.Equal(esperado, DdlDirectorios.EsUsuarioProtegido(usuario, mantenido, sesion));

    [Fact]
    public void Grant_solo_otorga_read_y_write_a_usuario_validado() =>
        Assert.Equal("GRANT READ, WRITE ON DIRECTORY DIR_RESPALDOS2 TO OPERADOR", DdlDirectorios.OtorgarLecturaEscritura("dir_respaldos2", "operador"));

    [Fact]
    public void Registrar_usa_merge_parametrizado_y_reactiva_sin_cambiar_identidad()
    {
        Assert.Contains(":nombre", SqlDirectorios.Registrar);
        Assert.Contains(":ruta", SqlDirectorios.Registrar);
        Assert.Contains("WHEN MATCHED THEN UPDATE SET d.ruta_so = s.ruta_so, d.activo = 'S'", SqlDirectorios.Registrar);
        Assert.DoesNotContain("SET d.id_directorio", SqlDirectorios.Registrar);
        Assert.DoesNotContain("fecha_registro", SqlDirectorios.Registrar.ToLowerInvariant());
    }

    [Fact]
    public void Verificacion_de_privilegios_admin_bd_se_limita_a_grants_directos_o_publicos()
    {
        Assert.Contains("grantee IN ('ADMIN_BD', 'PUBLIC')", SqlDirectorios.PrivilegiosDirectosOPublicosAdminBd);
        Assert.Contains("privilege IN ('READ', 'WRITE')", SqlDirectorios.PrivilegiosDirectosOPublicosAdminBd);
        Assert.DoesNotContain("dba_role_privs", SqlDirectorios.PrivilegiosDirectosOPublicosAdminBd, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eliminar_solo_construye_drop_sin_operaciones_de_archivos() =>
        Assert.Equal("DROP DIRECTORY DIR_RESPALDOS2", DdlDirectorios.Eliminar("dir_respaldos2"));

    [Theory]
    [InlineData("DIR_RESPALDOS")]
    [InlineData("DATA_PUMP_DIR")]
    [InlineData("DIR_RESPALDOS3")]
    public void Ddl_riesgoso_solo_admite_el_directorio_aislado_de_demostracion(string directorio)
    {
        Assert.Throws<ArgumentException>(() => DdlDirectorios.Crear(directorio, @"C:\respaldos_bd2\prueba"));
        Assert.Throws<ArgumentException>(() => DdlDirectorios.Eliminar(directorio));
        Assert.Throws<ArgumentException>(() => DdlDirectorios.OtorgarLecturaEscritura(directorio, "OPERADOR"));
    }

    [Theory]
    [InlineData("COMPLETADO", true)]
    [InlineData("EN_PROCESO", true)]
    [InlineData("ERROR", true)]
    [InlineData("ELIMINADO", false)]
    [InlineData(null, true)]
    [InlineData("ESTADO_DESCONOCIDO", true)]
    public void EstadoRespaldoImpideEliminar_es_conservador(string? estado, bool esperado) =>
        Assert.Equal(esperado, DdlDirectorios.EstadoRespaldoImpideEliminar(estado));

    [Theory]
    [InlineData(0, 0, 0, 0, false)] // solo respaldos ELIMINADO
    [InlineData(1, 0, 0, 0, true)]  // COMPLETADO, EN_PROCESO, ERROR, NULL o desconocido
    [InlineData(0, 1, 0, 0, true)]  // restauración activa
    [InlineData(0, 0, 1, 0, true)]  // programación activa
    [InlineData(0, 0, 0, 1, true)]  // job Data Pump ejecutando
    public void DependenciasEliminacion_bloquea_historial_y_dependencias_activas(
        int respaldos, int restauraciones, int programaciones, int jobs, bool esperado) =>
        Assert.Equal(esperado, new DependenciasEliminacion(respaldos, restauraciones, programaciones, jobs).ImpideEliminar);

    [Fact]
    public void ConsultaEliminar_bloquea_respaldos_historicos_y_dependencias_activas()
    {
        Assert.Contains("NVL(r.estado, '?') <> 'ELIMINADO'", SqlDirectorios.EstadoEliminar);
        Assert.Contains("NVL(x.estado, '?') = 'EN_PROCESO'", SqlDirectorios.EstadoEliminar);
        Assert.Contains("p.id_directorio = c.id_directorio AND p.activo = 'S'", SqlDirectorios.EstadoEliminar);
        Assert.Contains("j.state = 'EXECUTING'", SqlDirectorios.EstadoEliminar);
    }

    [Fact]
    public void ConsultaEliminar_expone_los_aliases_que_lee_la_interfaz()
    {
        var sql = SqlDirectorios.EstadoEliminar;

        foreach (var alias in new[] { "RESPALDOS_RESTAURABLES", "RESTAURACIONES_ACTIVAS", "PROGRAMACIONES_ACTIVAS", "JOBS_EJECUTANDO" })
            Assert.Contains($"AS {alias}", sql, StringComparison.OrdinalIgnoreCase);
    }
}

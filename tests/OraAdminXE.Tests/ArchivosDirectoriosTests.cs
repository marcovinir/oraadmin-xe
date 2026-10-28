using System.Text;
using OraAdminXE.Modulos.Directorios;

namespace OraAdminXE.Tests;

/// <summary>Reglas puras de #18: no abre Oracle ni crea, mueve o borra archivos.</summary>
public class ArchivosDirectoriosTests
{
    [Theory]
    [InlineData("EXP_SCHEMA_20261028.DMP")]
    [InlineData("copia segura.dMp")]
    [InlineData("A-1_$.dmp")]
    public void Nombre_dump_seguro_acepta_basename_dmp(string nombre) =>
        Assert.Equal(nombre, ArchivosDirectorios.ValidarNombreDump(nombre));

    [Theory]
    [InlineData("")]
    [InlineData("..\\otro.dmp")]
    [InlineData("C:\\respaldos\\otro.dmp")]
    [InlineData("sub/otro.dmp")]
    [InlineData("archivo.log")]
    [InlineData("CON.dmp")]
    [InlineData("a?.dmp")]
    [InlineData("dos..puntos.dmp")]
    public void Nombre_dump_seguro_rechaza_rutas_y_nombres_peligrosos(string nombre) =>
        Assert.Throws<ArgumentException>(() => ArchivosDirectorios.ValidarNombreDump(nombre));

    [Theory]
    [InlineData("x.DMP", true)]
    [InlineData("x.log", true)]
    [InlineData("x.txt", false)]
    public void Solo_extensiones_dmp_y_log_son_listables(string nombre, bool esperado) =>
        Assert.Equal(esperado, ArchivosDirectorios.EsExtensionPermitida(nombre));

    [Fact]
    public void Asociación_es_case_insensitive_pero_nunca_cruza_directorios()
    {
        Assert.True(ArchivosDirectorios.CoincideAsociacion(8, 8, "EXP_A.DMP", "exp_a.dmp"));
        Assert.False(ArchivosDirectorios.CoincideAsociacion(8, 9, "EXP_A.DMP", "exp_a.dmp"));
    }

    [Theory]
    [InlineData("DIR_RESPALDOS", true)]
    [InlineData("DIR_RESPALDOS2", true)]
    [InlineData("DATA_PUMP_DIR", false)]
    [InlineData("DIRECTORIO_EXTERNO", false)]
    public void Solo_directorios_dedicados_del_proyecto_son_mutables(string directorio, bool esperado) =>
        Assert.Equal(esperado, ArchivosDirectorios.EsDirectorioGestionado(directorio));

    [Theory]
    [InlineData("COMPLETADO", false, false, false, true)]
    [InlineData("EN_PROCESO", false, false, false, false)]
    [InlineData("ERROR", false, false, false, false)]
    [InlineData("COMPLETADO", true, false, false, false)]
    [InlineData("COMPLETADO", false, true, false, false)]
    [InlineData("COMPLETADO", false, false, true, false)]
    public void Mutacion_solo_autoriza_respaldo_completado_sin_operaciones_activas(string estado, bool dataPump, bool scheduler, bool restauracion, bool esperado) =>
        Assert.Equal(esperado, ArchivosDirectorios.PuedeMutarDump(estado, dataPump, scheduler, restauracion));

    [Fact]
    public void Reconciliacion_no_confunde_error_de_inspeccion_con_archivo_ausente()
    {
        Assert.True(ArchivosDirectorios.PuedeReconciliarAusente(EstadoInspeccionDirectorio.Correcto, "COMPLETADO", false, false, false, false));
        Assert.False(ArchivosDirectorios.PuedeReconciliarAusente(EstadoInspeccionDirectorio.AccesoDenegado, "COMPLETADO", false, false, false, false));
        Assert.False(ArchivosDirectorios.PuedeReconciliarAusente(EstadoInspeccionDirectorio.Correcto, "COMPLETADO", true, false, false, false));
        Assert.False(ArchivosDirectorios.PuedeReconciliarAusente(EstadoInspeccionDirectorio.Correcto, "EN_PROCESO", false, false, false, false));
    }

    [Theory]
    [InlineData(@"\\servidor\respaldos")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\respaldos\..\otro")]
    public void Ruta_lectura_rechaza_unc_raiz_y_traversal(string ruta) =>
        Assert.Throws<ArgumentException>(() => ArchivosDirectorios.ValidarRutaLectura(ruta));

    [Fact]
    public void Log_utf8_con_bom_se_decodifica_y_reporta_truncamiento()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("línea válida")).ToArray();
        var log = ArchivosDirectorios.DecodificarLog(bytes, truncado: true);
        Assert.Equal("línea válida", log.Contenido);
        Assert.True(log.Truncado);
    }

    [Fact]
    public void Log_utf16_y_bytes_invalidos_no_ejecutan_ni_lanzan()
    {
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("log Oracle")).ToArray();
        Assert.Equal("log Oracle", ArchivosDirectorios.DecodificarLog(utf16, false).Contenido);
        Assert.NotNull(ArchivosDirectorios.DecodificarLog([0xFF, 0xFE, 0x00], false).Contenido);
    }

    [Fact]
    public void SQL_de_acciones_usa_binds_y_update_condicional()
    {
        Assert.Contains(":dir_origen", SqlDirectorios.RenombrarArchivoFisico);
        Assert.Contains(":archivo_destino", SqlDirectorios.RenombrarArchivoFisico);
        Assert.Contains("FALSE", SqlDirectorios.RenombrarArchivoFisico);
        Assert.Contains(":directorio", SqlDirectorios.EliminarArchivoFisico);
        Assert.Contains("id_respaldo = :id_respaldo", SqlDirectorios.RenombrarDumpCatalogo);
        Assert.Contains("id_directorio = :id_directorio", SqlDirectorios.RenombrarDumpCatalogo);
        Assert.Contains("estado = 'COMPLETADO'", SqlDirectorios.MarcarRespaldoEliminado);
        Assert.Contains("UPPER(archivo_dump) = UPPER(:archivo_dump)", SqlDirectorios.MarcarRespaldoEliminado);
    }

    [Fact]
    public void SQL_de_operacion_bloquea_por_datapump_scheduler_y_restauracion()
    {
        Assert.Contains("dba_datapump_jobs", SqlDirectorios.RespaldoParaOperacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dba_scheduler_running_jobs", SqlDirectorios.RespaldoParaOperacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restauracion_activa", SqlDirectorios.RespaldoParaOperacion, StringComparison.OrdinalIgnoreCase);
    }
}

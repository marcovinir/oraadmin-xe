using OraAdminXE.Modulos.Tablespaces;

namespace OraAdminXE.Tests;

public class DdlTablespacesTests
{
    private const string Directorio = "C:\\app\\oracle\\oradata\\XE\\XEPDB1";
    private const string Archivo = Directorio + "\\TS_VENTAS01.DBF";

    [Fact]
    public void Genera_DDL_de_crear_agregar_resize_y_autoextend()
    {
        Assert.Equal("CREATE TABLESPACE TS_VENTAS DATAFILE 'C:\\app\\oracle\\oradata\\XE\\XEPDB1\\TS_VENTAS01.DBF' SIZE 50M AUTOEXTEND ON NEXT 10M MAXSIZE 500M",
            DdlTablespaces.Crear("ts_ventas", Archivo, 50, true, 10, 500));
        Assert.Equal("ALTER TABLESPACE TS_VENTAS ADD DATAFILE 'C:\\app\\oracle\\oradata\\XE\\XEPDB1\\TS_VENTAS02.DBF' SIZE 20M",
            DdlTablespaces.AgregarDatafile("ts_ventas", Directorio + "\\TS_VENTAS02.DBF", 20));
        Assert.Equal("ALTER DATABASE DATAFILE 'C:\\app\\oracle\\oradata\\XE\\XEPDB1\\TS_VENTAS01.DBF' RESIZE 100M",
            DdlTablespaces.RedimensionarDatafile(Archivo, 100));
        Assert.Equal("ALTER DATABASE DATAFILE 'C:\\app\\oracle\\oradata\\XE\\XEPDB1\\TS_VENTAS01.DBF' AUTOEXTEND ON NEXT 10M MAXSIZE 500M",
            DdlTablespaces.CambiarAutoextend(Archivo, true, 50, 10, 500));
        Assert.Equal("ALTER DATABASE DATAFILE 'C:\\app\\oracle\\oradata\\XE\\XEPDB1\\TS_VENTAS01.DBF' AUTOEXTEND OFF",
            DdlTablespaces.CambiarAutoextend(Archivo, false, 50));
    }

    [Theory]
    [InlineData(EstadoTablespace.Online, "ALTER TABLESPACE TS_VENTAS ONLINE")]
    [InlineData(EstadoTablespace.Offline, "ALTER TABLESPACE TS_VENTAS OFFLINE NORMAL")]
    [InlineData(EstadoTablespace.SoloLectura, "ALTER TABLESPACE TS_VENTAS READ ONLY")]
    [InlineData(EstadoTablespace.LecturaEscritura, "ALTER TABLESPACE TS_VENTAS READ WRITE")]
    public void Genera_estados_seguros(EstadoTablespace destino, string esperado) =>
        Assert.Equal(esperado, DdlTablespaces.CambiarEstado("ts_ventas", destino));

    [Fact]
    public void Drop_siempre_usa_contents_y_datafiles() =>
        Assert.Equal("DROP TABLESPACE TS_VENTAS INCLUDING CONTENTS AND DATAFILES", DdlTablespaces.Eliminar("ts_ventas"));

    [Theory]
    [InlineData("SYSTEM", "PERMANENT")]
    [InlineData("SYSAUX", "PERMANENT")]
    [InlineData("USERS", "PERMANENT")]
    [InlineData("UNDOTBS1", "UNDO")]
    [InlineData("TEMP", "TEMPORARY")]
    [InlineData("APP_DATOS", "PERMANENT")]
    public void Protege_tablespaces_criticos_y_de_aplicacion_no_verificados(string nombre, string contenido) =>
        Assert.False(DdlTablespaces.EsElegibleParaAdministrar(nombre, contenido));

    [Theory]
    [InlineData("TS_VENTAS", true)]
    [InlineData("ORAADMIN_TEST_A", true)]
    [InlineData("ORAADMIN_TEST_", false)]
    [InlineData("APP_DATOS", false)]
    [InlineData("TS_VENTAS;DROP", false)]
    public void Solo_los_nombres_de_prueba_son_autorizables(string nombre, bool esperado) =>
        Assert.Equal(esperado, DdlTablespaces.EsNombrePruebaAutorizado(nombre));

    [Fact]
    public void Drop_exige_procedencia_dependencias_y_tablespace_elegible()
    {
        const string nombre = "ORAADMIN_TEST_DROP_9";
        Assert.False(DdlTablespaces.PuedeEliminar(nombre, "PERMANENT", 0, 0));
        DdlTablespaces.RegistrarCreadoEnSesion(nombre);
        try
        {
            Assert.True(DdlTablespaces.PuedeEliminar(nombre, "PERMANENT", 0, 0));
            Assert.False(DdlTablespaces.PuedeEliminar(nombre, "PERMANENT", 1, 0));
            Assert.False(DdlTablespaces.PuedeEliminar(nombre, "PERMANENT", 0, 1));
            Assert.False(DdlTablespaces.PuedeEliminar(nombre, "PERMANENT", 0, 0, 1));
            Assert.False(DdlTablespaces.PuedeEliminar(nombre, "UNDO", 0, 0));
        }
        finally { DdlTablespaces.OlvidarCreadoEnSesion(nombre); }
    }

    [Theory]
    [InlineData("ONLINE", EstadoTablespace.Offline, true)]
    [InlineData("OFFLINE", EstadoTablespace.Online, true)]
    [InlineData("ONLINE", EstadoTablespace.SoloLectura, true)]
    [InlineData("READ ONLY", EstadoTablespace.LecturaEscritura, true)]
    [InlineData("OFFLINE", EstadoTablespace.SoloLectura, false)]
    [InlineData("ONLINE", EstadoTablespace.Online, false)]
    public void Transiciones_solo_permiten_estados_validos(string actual, EstadoTablespace destino, bool esperado) =>
        Assert.Equal(esperado, DdlTablespaces.TransicionValida(actual, destino));

    [Theory]
    [InlineData(0, 10, 100)]
    [InlineData(50, 0, 100)]
    [InlineData(50, 10, 59)]
    public void Autoextend_rechaza_tamanos_inseguros(long actual, long next, long max) =>
        Assert.Throws<ArgumentException>(() => DdlTablespaces.ValidarAutoextend(actual, next, max));

    [Theory]
    [InlineData("C:\\app\\oracle\\oradata\\XE\\XEPDB1\\TS_VENTAS01.DBF", true)]
    [InlineData("\\\\servidor\\datos\\x.dbf", false)]
    [InlineData("D:\\otro\\x.dbf", false)]
    [InlineData("C:\\app\\oracle\\oradata\\XE\\XEPDB1\\..\\x.dbf", false)]
    [InlineData("C:\\Windows\\x.dbf", false)]
    [InlineData("C:\\app\\oracle\\oradata\\XE\\XEPDB1\\x.txt", false)]
    public void Valida_ruta_windows_bajo_directorio_verificado(string ruta, bool valida)
    {
        if (valida) Assert.Equal(ruta, DdlTablespaces.ValidarRutaDatafile(ruta, Directorio, verificarSistemaArchivos: false));
        else Assert.Throws<ArgumentException>(() => DdlTablespaces.ValidarRutaDatafile(ruta, Directorio, verificarSistemaArchivos: false));
    }

    [Fact]
    public void Previene_colisiones_registradas_sin_distinguir_mayusculas() =>
        Assert.True(DdlTablespaces.RutaEstaRegistrada(Archivo.ToLowerInvariant(), [Archivo]));

    [Fact]
    public void Builder_rechaza_extensiones_y_rutas_del_sistema() 
    {
        Assert.Throws<ArgumentException>(() => DdlTablespaces.Crear("TS_VENTAS", Directorio + "\\x.txt", 50, false, null, null));
        Assert.Throws<ArgumentException>(() => DdlTablespaces.Crear("TS_VENTAS", "C:\\Windows\\x.dbf", 50, false, null, null));
    }

    [Theory]
    [InlineData("TS_VENTAS; DROP TABLESPACE SYSTEM")]
    [InlineData("C:\\app\\oracle\\oradata\\XE\\XEPDB1\\x.dbf'; DROP TABLESPACE SYSTEM --")]
    public void Rechaza_entradas_maliciosas(string entrada)
    {
        Assert.Throws<ArgumentException>(() => DdlTablespaces.Crear(entrada, Archivo, 50, false, null, null));
        Assert.Throws<ArgumentException>(() => DdlTablespaces.RedimensionarDatafile(entrada, 50));
    }
}

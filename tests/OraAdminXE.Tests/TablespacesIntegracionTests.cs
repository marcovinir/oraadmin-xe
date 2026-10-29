using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.Modulos.Tablespaces;

namespace OraAdminXE.Tests;

/// <summary>Consultas estrictamente de lectura para la pantalla de Tablespaces (issue #15).</summary>
public class TablespacesIntegracionTests : IAsyncLifetime
{
    private readonly BaseDatos _bd = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OracleFactAttribute.VariableClave)))
            await _bd.ConectarAsync(new ConfigConexion(), OracleFactAttribute.Clave);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [OracleFact]
    public async Task Uso_muestra_tablespaces_base_y_porcentajes_reales()
    {
        var tabla = await _bd.ConsultarAsync(SqlTablespaces.Uso);
        var nombres = tabla.AsEnumerable().Select(f => f.Field<string>("TABLESPACE_NAME"))
            .Where(nombre => nombre is not null).Select(nombre => nombre!).ToHashSet();
        Assert.Superset(new HashSet<string> { "SYSTEM", "SYSAUX", "UNDOTBS1", "USERS", "TEMP" }, nombres);

        foreach (var fila in tabla.AsEnumerable())
        {
            Assert.InRange(Convert.ToDouble(fila["PCT_USADO"]), 0, 100);
            Assert.Contains((string)fila["AUTOEXTEND"], ["YES", "NO", "MIXED"]);
        }

        var users = tabla.AsEnumerable().Single(f => f.Field<string>("TABLESPACE_NAME") == "USERS");
        Assert.InRange(Convert.ToDouble(users["PCT_USADO"]), 0, 100);

        var temp = tabla.AsEnumerable().Single(f => f.Field<string>("TABLESPACE_NAME") == "TEMP");
        Assert.Equal("TEMPORARY", temp.Field<string>("CONTENTS"));
        Assert.Equal("ONLINE", temp.Field<string>("STATUS"));
    }

    [OracleFact]
    public async Task Archivos_corresponden_al_tablespace_seleccionado()
    {
        var datafiles = await _bd.ConsultarAsync(SqlTablespaces.Archivos, Parametro.De("tablespace", "USERS"));
        Assert.NotEmpty(datafiles.Rows);
        Assert.All(datafiles.AsEnumerable(), fila =>
        {
            Assert.Equal("DATAFILE", fila.Field<string>("TIPO_ARCHIVO"));
            Assert.False(string.IsNullOrWhiteSpace(fila.Field<string>("FILE_NAME")));
            Assert.True(Convert.ToInt64(fila["FILE_ID"]) > 0);
            Assert.True(Convert.ToDouble(fila["TAMANO_MB"]) > 0);
            Assert.True(Convert.ToDouble(fila["MAX_MB"]) >= Convert.ToDouble(fila["TAMANO_MB"]));
            Assert.InRange(Convert.ToDouble(fila["PCT_USADO"]), 0, 100);
            Assert.Contains((string)fila["AUTOEXTENSIBLE"], ["YES", "NO"]);
            Assert.Equal("ONLINE", fila.Field<string>("ESTADO"));
        });

        var tempfiles = await _bd.ConsultarAsync(SqlTablespaces.Archivos, Parametro.De("tablespace", "TEMP"));
        Assert.NotEmpty(tempfiles.Rows);
        Assert.All(tempfiles.AsEnumerable(), fila =>
        {
            Assert.Equal("TEMPFILE", fila.Field<string>("TIPO_ARCHIVO"));
            Assert.False(string.IsNullOrWhiteSpace(fila.Field<string>("FILE_NAME")));
            Assert.True(Convert.ToInt64(fila["FILE_ID"]) > 0);
            Assert.True(Convert.ToDouble(fila["TAMANO_MB"]) > 0);
            Assert.True(Convert.ToDouble(fila["MAX_MB"]) >= Convert.ToDouble(fila["TAMANO_MB"]));
            Assert.InRange(Convert.ToDouble(fila["PCT_USADO"]), 0, 100);
            Assert.Contains((string)fila["AUTOEXTENSIBLE"], ["YES", "NO"]);
            Assert.Equal("ONLINE", fila.Field<string>("ESTADO"));
        });
    }

    [OracleFact]
    public async Task Metadatos_administrativos_son_solo_lectura_y_exactos()
    {
        var privilegios = await _bd.ConsultarAsync(SqlTablespaces.PrivilegiosSesion);
        Assert.Contains(privilegios.AsEnumerable(), fila => fila.Field<string>("CON_NAME") == "XEPDB1");

        var users = await _bd.ConsultarAsync(SqlTablespaces.MetadatosTablespace, Parametro.De("tablespace", "USERS"));
        Assert.Single(users.Rows);
        Assert.Equal("PERMANENT", users.Rows[0].Field<string>("CONTENTS"));

        var ruta = await _bd.EscalarAsync(SqlTablespaces.DatafileUsers) as string;
        Assert.False(string.IsNullOrWhiteSpace(ruta));
        var destinoOmg = await _bd.EscalarAsync(SqlTablespaces.DestinoOracleManagedFiles);
        Assert.True(destinoOmg is null or string);
        var blockSize = Convert.ToInt64(await _bd.EscalarAsync(SqlTablespaces.BloquePredeterminado));
        Assert.True(blockSize > 0);
        var dependencias = await _bd.ConsultarAsync(SqlTablespaces.DependenciasEliminar, Parametro.De("tablespace", "USERS"));
        Assert.Single(dependencias.Rows);
        Assert.All(["SEGMENTOS", "USUARIOS_ASIGNADOS", "CUOTAS", "PREDETERMINADOS_PDB", "OBJETOS_ASIGNADOS"],
            columna => Assert.True(dependencias.Columns.Contains(columna)));
        var archivos = await _bd.ConsultarAsync(SqlTablespaces.Archivos, Parametro.De("tablespace", "USERS"));
        var fila = archivos.AsEnumerable().First();
        var exacto = await _bd.ConsultarAsync(SqlTablespaces.MetadatosDatafile,
            Parametro.De("tablespace", "USERS"), Parametro.De("file_id", Convert.ToInt64(fila["FILE_ID"])), Parametro.De("file_name", fila.Field<string>("FILE_NAME")));
        Assert.Single(exacto.Rows);
        Assert.True(Convert.ToInt64(exacto.Rows[0]["BYTES"]) > 0);
    }
}

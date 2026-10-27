using OraAdminXE.Datos;

namespace OraAdminXE.Tests;

public class SentenciasTests
{
    [Theory]
    [InlineData("CREATE TABLE t (id NUMBER);", "CREATE TABLE t (id NUMBER)")]
    [InlineData("  DROP USER juan CASCADE ;  \r\n", "DROP USER juan CASCADE")]
    [InlineData("SELECT 1 FROM dual", "SELECT 1 FROM dual")]
    [InlineData("ALTER TABLESPACE x OFFLINE;\r\n/", "ALTER TABLESPACE x OFFLINE")]
    public void Normalizar_quita_punto_y_coma_final_en_sql(string entrada, string esperado) =>
        Assert.Equal(esperado, Sentencias.Normalizar(entrada));

    [Theory]
    [InlineData("BEGIN NULL; END;", "BEGIN NULL; END;")]
    [InlineData("begin dbms_stats.gather_schema_stats('X'); end;\n/", "begin dbms_stats.gather_schema_stats('X'); end;")]
    [InlineData("DECLARE h NUMBER; BEGIN h := 1; END;", "DECLARE h NUMBER; BEGIN h := 1; END;")]
    public void Normalizar_conserva_el_punto_y_coma_de_plsql(string entrada, string esperado) =>
        Assert.Equal(esperado, Sentencias.Normalizar(entrada));

    [Theory]
    [InlineData("CREATE USER juan IDENTIFIED BY \"Clave_2026\" DEFAULT TABLESPACE users",
                "CREATE USER juan IDENTIFIED BY \"********\" DEFAULT TABLESPACE users")]
    [InlineData("alter user juan identified by Secreta1", "alter user juan identified by \"********\"")]
    [InlineData("ALTER USER juan ACCOUNT LOCK", "ALTER USER juan ACCOUNT LOCK")]
    public void OcultarClaves_enmascara_contrasenas(string entrada, string esperado) =>
        Assert.Equal(esperado, Sentencias.OcultarClaves(entrada));

    [Fact]
    public void EsPlSql_reconoce_bloques()
    {
        Assert.True(Sentencias.EsPlSql("  begin null; end;"));
        Assert.True(Sentencias.EsPlSql("DECLARE x NUMBER; BEGIN NULL; END;"));
        Assert.False(Sentencias.EsPlSql("SELECT * FROM dual"));
    }
}

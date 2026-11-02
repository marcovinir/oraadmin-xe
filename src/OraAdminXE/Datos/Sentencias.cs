using System.Text.RegularExpressions;

namespace OraAdminXE.Datos;

/// <summary>Utilidades sobre el texto de las sentencias antes de ejecutarlas o mostrarlas.</summary>
public static partial class Sentencias
{
    [GeneratedRegex(@"^\s*(BEGIN|DECLARE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InicioPlSql();

    [GeneratedRegex(@"(IDENTIFIED\s+BY\s+)(""[^""]*""|\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex Clave();

    /// <summary>¿Es un bloque anónimo PL/SQL (BEGIN … END; o DECLARE …)?</summary>
    public static bool EsPlSql(string sql) => InicioPlSql().IsMatch(sql);

    /// <summary>
    /// Deja la sentencia lista para ODP.NET: quita espacios, una «/» final de SQL*Plus y,
    /// si no es PL/SQL, el «;» final (ODP.NET lo rechaza con ORA-00933/ORA-00911).
    /// </summary>
    public static string Normalizar(string sql)
    {
        var texto = sql.Trim();
        if (texto.EndsWith('/'))
            texto = texto[..^1].TrimEnd();
        if (!EsPlSql(texto) && texto.EndsWith(';'))
            texto = texto[..^1].TrimEnd();
        return texto;
    }

    /// <summary>Reemplaza las contraseñas de <c>IDENTIFIED BY</c> por asteriscos (bitácora y «Ver SQL»).</summary>
    public static string OcultarClaves(string sql) => Clave().Replace(sql, "$1\"********\"");
}

namespace OraAdminXE.Modulos.Bitacora;

/// <summary>Consultas de la Bitácora (probadas en Oracle 21c XE, PDB XEPDB1).</summary>
public static class SqlBitacora
{
    public const int Maximo = 1000;

    public const string Modulos = "SELECT nombre FROM admin_bd.modulo ORDER BY nombre";

    /// <summary>
    /// Filtros opcionales: un bind NULL significa «todos». :hasta es inclusivo (se le suma un día).
    /// </summary>
    public const string Buscar = """
        SELECT id_bitacora, fecha, modulo, usuario_bd, accion, resultado, mensaje, sentencia
        FROM admin_bd.v_bitacora
        WHERE fecha >= :desde AND fecha < :hasta + 1
          AND (:modulo IS NULL OR modulo = :modulo)
          AND (:resultado IS NULL OR resultado = :resultado)
          AND (:texto IS NULL OR UPPER(accion) LIKE '%' || UPPER(:texto) || '%')
        ORDER BY fecha DESC, id_bitacora DESC
        FETCH FIRST 1000 ROWS ONLY
        """;
}

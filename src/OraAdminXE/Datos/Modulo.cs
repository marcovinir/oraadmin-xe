namespace OraAdminXE.Datos;

/// <summary>
/// Módulos de la app. Los nombres coinciden con las filas de la tabla MODULO
/// (sql/01_modelo_3fn.sql); la bitácora busca el id por <c>ToString().ToUpper()</c>.
/// </summary>
public enum Modulo
{
    General,
    Tablespaces,
    Tuning,
    Performance,
    Auditoria,
    Seguridad,
    Respaldos,
    Recuperacion,
    Directorios
}

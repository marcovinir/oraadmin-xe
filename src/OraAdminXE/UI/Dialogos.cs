namespace OraAdminXE.UI;

/// <summary>
/// Diálogos comunes. Todas las pantallas los usan para que el comportamiento sea igual en toda la app.
/// PENDIENTE: issue #4.
/// </summary>
public static class Dialogos
{
    /// <summary>
    /// Muestra la sentencia exacta (fuente monoespaciada, con botón Copiar) y pregunta si se ejecuta.
    /// Si <paramref name="destructiva"/> es true, el botón de aceptar es rojo y dice «Ejecutar de todos modos».
    /// Devuelve true si el usuario confirma.
    /// </summary>
    public static bool ConfirmarSql(IWin32Window? duenio, string titulo, string sql, bool destructiva = false) =>
        throw new NotImplementedException("Pendiente: issue #4 (diálogos comunes)");

    /// <summary>Solo muestra el SQL (botón «Ver SQL»), sin ejecutar nada.</summary>
    public static void VerSql(IWin32Window? duenio, string titulo, string sql) =>
        throw new NotImplementedException("Pendiente: issue #4 (diálogos comunes)");

    /// <summary>
    /// Muestra un error. Para <c>OracleException</c> enseña el código ORA-xxxxx y el texto completos,
    /// nunca un «Error» genérico.
    /// </summary>
    public static void Error(IWin32Window? duenio, Exception ex) =>
        throw new NotImplementedException("Pendiente: issue #4 (diálogos comunes)");

    /// <summary>Mensaje de éxito breve.</summary>
    public static void Exito(IWin32Window? duenio, string mensaje) =>
        throw new NotImplementedException("Pendiente: issue #4 (diálogos comunes)");
}

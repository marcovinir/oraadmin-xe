namespace OraAdminXE.Datos;

/// <summary>Punto de acceso global a la conexión activa (se llena en el login).</summary>
public static class Sesion
{
    public static IBaseDatos Bd { get; set; } = new BaseDatos();
}

namespace OraAdminXE.Datos;

/// <summary>
/// Datos de conexión que se recuerdan entre ejecuciones en <c>conexion.json</c>
/// (junto al .exe, ignorado por git). La contraseña NUNCA se guarda.
/// </summary>
public sealed class ConfigConexion
{
    public string Host { get; set; } = "localhost";
    public int Puerto { get; set; } = 1521;
    public string Servicio { get; set; } = "XEPDB1";
    public string Usuario { get; set; } = "ADMIN_BD";

    /// <summary>Easy Connect: host:puerto/servicio.</summary>
    public string DataSource => $"{Host}:{Puerto}/{Servicio}";
}

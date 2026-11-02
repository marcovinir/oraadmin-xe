using System.Text.Json;
using System.Text.Json.Serialization;

namespace OraAdminXE.Datos;

/// <summary>
/// Datos de conexión que se recuerdan entre ejecuciones en <c>conexion.json</c>
/// (junto al .exe, ignorado por git). La contraseña NUNCA se guarda.
/// </summary>
public sealed class ConfigConexion
{
    private static readonly JsonSerializerOptions Opciones = new() { WriteIndented = true };

    public string Host { get; set; } = "localhost";
    public int Puerto { get; set; } = 1521;
    public string Servicio { get; set; } = "XEPDB1";
    public string Usuario { get; set; } = "ADMIN_BD";

    /// <summary>Easy Connect: host:puerto/servicio.</summary>
    [JsonIgnore]
    public string DataSource => $"{Host}:{Puerto}/{Servicio}";

    /// <summary>Ruta por defecto: <c>conexion.json</c> en la carpeta del ejecutable.</summary>
    public static string RutaPorDefecto => Path.Combine(AppContext.BaseDirectory, "conexion.json");

    /// <summary>Lee la última conexión usada. Si no existe o está dañada, devuelve los valores por defecto.</summary>
    public static ConfigConexion Cargar(string? ruta = null)
    {
        try
        {
            var texto = File.ReadAllText(ruta ?? RutaPorDefecto);
            return JsonSerializer.Deserialize<ConfigConexion>(texto) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    /// <summary>Guarda host, puerto, servicio y usuario (sin contraseña). Si no se puede escribir, no pasa nada.</summary>
    public void Guardar(string? ruta = null)
    {
        try
        {
            File.WriteAllText(ruta ?? RutaPorDefecto, JsonSerializer.Serialize(this, Opciones));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Recordar la conexión es una comodidad; no debe impedir el login.
        }
    }
}

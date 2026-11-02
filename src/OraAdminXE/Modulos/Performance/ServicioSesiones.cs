using System.Data;

namespace OraAdminXE.Modulos.Performance;

/// <summary>
/// Lógica de sesiones y bloqueos (issue #23).
/// Separada de la UI para poder probarla con pruebas unitarias sin conexión a Oracle.
/// </summary>
public static class ServicioSesiones
{
    /// <summary>
    /// Genera la sentencia para matar una sesión. Valida que los identificadores sean positivos
    /// y que no se intente matar la propia sesión de la aplicación.
    /// </summary>
    public static string MatarSesion(int sid, int serial, bool esLaApp)
    {
        if (esLaApp)
            throw new InvalidOperationException("No se puede matar la sesión de la propia aplicación.");
        if (sid <= 0 || serial < 0)
            throw new ArgumentException("El SID debe ser positivo y el número de serie no puede ser negativo.");

        return $"ALTER SYSTEM KILL SESSION '{sid},{serial}' IMMEDIATE";
    }

    /// <summary>Conjunto de SIDs que están bloqueando a alguna otra sesión.</summary>
    public static HashSet<int> ObtenerSidsBloqueadores(DataTable sesiones)
    {
        var bloqueadores = new HashSet<int>();
        foreach (DataRow fila in sesiones.Rows)
        {
            if (fila["BLOCKING_SESSION"] is not DBNull and not null &&
                Convert.ToInt32(fila["BLOCKING_SESSION"]) is int b && b > 0)
            {
                bloqueadores.Add(b);
            }
        }
        return bloqueadores;
    }

    /// <summary>
    /// Genera los mensajes descriptivos de bloqueos activos cruzando las sesiones con v$locked_object.
    /// Ejemplo: «SID 12 (JUAN) bloquea a SID 45 (DEMO_VENTAS) en DEMO_VENTAS.CLIENTE (ROW EXCLUSIVE)».
    /// </summary>
    public static List<string> FormatearBloqueos(DataTable sesiones, DataTable objetosBloqueados)
    {
        var resultados = new List<string>();

        // Mapa de objetos bloqueados por SID: SID -> lista de "OWNER.OBJECT_NAME (MODO)"
        var objetosPorSid = new Dictionary<int, List<string>>();
        foreach (DataRow fila in objetosBloqueados.Rows)
        {
            if (fila["SID"] is not DBNull and not null && Convert.ToInt32(fila["SID"]) is int sid)
            {
                var owner = fila["OWNER"]?.ToString() ?? "";
                var obj = fila["OBJECT_NAME"]?.ToString() ?? "";
                var modo = fila["MODO"]?.ToString() ?? "";
                var desc = string.IsNullOrEmpty(owner) ? obj : $"{owner}.{obj}";
                if (!string.IsNullOrEmpty(modo)) desc += $" ({modo})";

                if (!objetosPorSid.TryGetValue(sid, out var lista))
                {
                    lista = [];
                    objetosPorSid[sid] = lista;
                }
                lista.Add(desc);
            }
        }

        // Mapa de usuario por SID
        var usuariosPorSid = new Dictionary<int, string>();
        foreach (DataRow fila in sesiones.Rows)
        {
            if (fila["SID"] is not DBNull and not null && Convert.ToInt32(fila["SID"]) is int sid)
            {
                usuariosPorSid[sid] = fila["USERNAME"]?.ToString() ?? "sin usuario";
            }
        }

        // Para cada sesión que espera un bloqueo
        foreach (DataRow fila in sesiones.Rows)
        {
            if (fila["BLOCKING_SESSION"] is not DBNull and not null &&
                Convert.ToInt32(fila["BLOCKING_SESSION"]) is int sidBloqueador && sidBloqueador > 0)
            {
                var sidEsperando = Convert.ToInt32(fila["SID"]);
                var uEsperando = fila["USERNAME"]?.ToString() ?? "sin usuario";
                var uBloqueador = usuariosPorSid.TryGetValue(sidBloqueador, out var ub) ? ub : "desconocido";

                string enObjeto = "";
                if (objetosPorSid.TryGetValue(sidBloqueador, out var objBloq) && objBloq.Count > 0)
                    enObjeto = $"; objetos asociados al bloqueador: {string.Join(", ", objBloq)}";
                else if (objetosPorSid.TryGetValue(sidEsperando, out var objEsp) && objEsp.Count > 0)
                    enObjeto = $"; objetos asociados a la sesión en espera: {string.Join(", ", objEsp)}";

                resultados.Add($"⚠ SID {sidBloqueador} ({uBloqueador}) bloquea a SID {sidEsperando} ({uEsperando}){enObjeto}");
            }
        }

        return resultados;
    }
}

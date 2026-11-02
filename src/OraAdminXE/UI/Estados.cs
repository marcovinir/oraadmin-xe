namespace OraAdminXE.UI;

/// <summary>Significado visual de un estado (define el color de su pastilla).</summary>
public enum TipoEstado { Neutro, Ok, Advertencia, Peligro, Info, Acento }

/// <summary>Clasifica los textos de estado de Oracle y de la app para pintarlos siempre igual.</summary>
public static class Estados
{
    private static readonly HashSet<string> Buenos =
        ["OPEN", "ONLINE", "COMPLETADO", "COMPLETED", "OK", "YES", "SI", "VALID", "READ WRITE", "ACTIVE", "ENABLED", "EXECUTING"];
    private static readonly HashSet<string> Malos =
        ["ERROR", "OFFLINE", "INVALID", "FAILED", "UNUSABLE", "KILLED"];
    private static readonly HashSet<string> Atencion =
        ["EN_PROCESO", "EN PROCESO", "READ ONLY", "INACTIVE", "NOT RUNNING", "PENDIENTE"];
    private static readonly HashSet<string> Apagados = ["NO", "ELIMINADO", "DISABLED"];

    public static TipoEstado Clasificar(string texto)
    {
        var t = texto.Trim().ToUpperInvariant();
        if (t.Contains("LOCKED")) return TipoEstado.Peligro;          // LOCKED, LOCKED(TIMED), EXPIRED & LOCKED
        if (t.Contains("EXPIRED")) return TipoEstado.Advertencia;     // EXPIRED, EXPIRED(GRACE)
        if (Buenos.Contains(t)) return TipoEstado.Ok;
        if (Malos.Contains(t)) return TipoEstado.Peligro;
        if (Atencion.Contains(t)) return TipoEstado.Advertencia;
        if (Apagados.Contains(t)) return TipoEstado.Neutro;
        return TipoEstado.Info;
    }
}

using OraAdminXE.Modulos.Auditoria;
using OraAdminXE.Modulos.Bitacora;
using OraAdminXE.Modulos.Dashboard;
using OraAdminXE.Modulos.Directorios;
using OraAdminXE.Modulos.Performance;
using OraAdminXE.Modulos.Recuperacion;
using OraAdminXE.Modulos.Respaldos;
using OraAdminXE.Modulos.Seguridad;
using OraAdminXE.Modulos.Tablespaces;
using OraAdminXE.Modulos.Tuning;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos;

/// <summary>Una opción del menú lateral.</summary>
public sealed record EntradaModulo(string Icono, string Titulo, Func<ModuloBase> Crear);

/// <summary>
/// Orden del menú lateral. Cada pantalla vive en su propia carpeta de Modulos/
/// para que cada persona trabaje en archivos distintos y no haya conflictos en git.
/// </summary>
public static class RegistroModulos
{
    public static readonly IReadOnlyList<EntradaModulo> Todos =
    [
        new("⌂",  "Inicio",               () => new UcDashboard()),
        new("▤",  "Tablespaces",          () => new UcTablespaces()),
        new("⚡", "Tuning",               () => new UcTuning()),
        new("📈", "Performance",          () => new UcPerformance()),
        new("🔍", "Auditoría",            () => new UcAuditoria()),
        new("🔒", "Seguridad",            () => new UcSeguridad()),
        new("💾", "Respaldos",            () => new UcRespaldos()),
        new("↺",  "Recuperación",         () => new UcRecuperacion()),
        new("📁", "Directorios y archivos", () => new UcDirectorios()),
        new("📜", "Bitácora",             () => new UcBitacora()),
    ];
}

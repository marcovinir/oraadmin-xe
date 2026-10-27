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

/// <summary>Una opción del menú lateral. <paramref name="Icono"/> es un glifo de la fuente Segoe MDL2 Assets.</summary>
public sealed record EntradaModulo(string Icono, string Titulo, Func<ModuloBase> Crear);

/// <summary>
/// Orden del menú lateral. Cada pantalla vive en su propia carpeta de Modulos/
/// para que cada persona trabaje en archivos distintos y no haya conflictos en git.
/// </summary>
public static class RegistroModulos
{
    public static readonly IReadOnlyList<EntradaModulo> Todos =
    [
        new("\uE80F", "Inicio",                 () => new UcDashboard()),
        new("\uEDA2", "Tablespaces",            () => new UcTablespaces()),
        new("\uE945", "Tuning",                 () => new UcTuning()),
        new("\uE9D9", "Performance",            () => new UcPerformance()),
        new("\uE890", "Auditoría",              () => new UcAuditoria()),
        new("\uE72E", "Seguridad",              () => new UcSeguridad()),
        new("\uE74E", "Respaldos",              () => new UcRespaldos()),
        new("\uE81C", "Recuperación",           () => new UcRecuperacion()),
        new("\uE8B7", "Directorios y archivos", () => new UcDirectorios()),
        new("\uEA37", "Bitácora",               () => new UcBitacora()),
    ];
}

using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>Reglas compartidas por las barras de uso del módulo.</summary>
public static class IndicadorUsoTablespace
{
    public const double UmbralAmarillo = 70;
    public const double UmbralRojo = 85;

    /// <summary>Normaliza un valor para dibujarlo sin afirmar porcentajes fuera del rango útil.</summary>
    public static double? Normalizar(double? porcentaje) => porcentaje is not { } valor || !double.IsFinite(valor)
        ? null
        : Math.Clamp(valor, 0, 100);

    /// <summary>Verde &lt;70, amarillo de 70 a 85 inclusive y rojo &gt;85.</summary>
    public static TipoEstado Clasificar(double? porcentaje)
    {
        var valor = Normalizar(porcentaje);
        if (valor is null) return TipoEstado.Neutro;
        if (valor > UmbralRojo) return TipoEstado.Peligro;
        if (valor >= UmbralAmarillo) return TipoEstado.Advertencia;
        return TipoEstado.Ok;
    }
}

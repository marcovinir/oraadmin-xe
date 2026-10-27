namespace OraAdminXE.Tests;

/// <summary>
/// Prueba de integración contra Oracle 21c XE local (ADMIN_BD@localhost:1521/XEPDB1).
/// Se salta si no está definida la variable de entorno ORAADMIN_TEST_CLAVE con la contraseña de ADMIN_BD.
/// </summary>
public sealed class OracleFactAttribute : FactAttribute
{
    public const string VariableClave = "ORAADMIN_TEST_CLAVE";
    public const string VariableClaveDemo = "ORAADMIN_TEST_CLAVE_DEMO";

    public OracleFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VariableClave)))
            Skip = $"Defina {VariableClave} con la contraseña de ADMIN_BD para correr las pruebas contra Oracle.";
    }

    public static string Clave => Environment.GetEnvironmentVariable(VariableClave)!;
    public static string? ClaveDemo => Environment.GetEnvironmentVariable(VariableClaveDemo);
}

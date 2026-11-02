namespace OraAdminXE.Tests;

/// <summary>
/// Las pruebas que lanzan jobs de Data Pump corren una tras otra: en XE (2 hilos de CPU) varios jobs
/// arrancando a la vez a veces fallan con ORA-31626 y la prueba se vuelve intermitente.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionDataPump
{
    public const string Nombre = "Data Pump";
}

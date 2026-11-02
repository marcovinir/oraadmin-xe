using System.Diagnostics;
using OraAdminXE.Datos;

namespace OraAdminXE.UI;

/// <summary>
/// Red de seguridad para toda la app: una excepción que se escapa de un evento (sobre todo de los
/// <c>async void</c> de botones y grillas) se muestra con <see cref="Dialogos.Error"/> y queda en la bitácora,
/// en lugar de cerrar el programa. No reemplaza los try/catch de cada pantalla: es lo que atrapa lo que se olvidó.
/// </summary>
public static class ManejadorErrores
{
    private static Action<Exception>? _mostrar;
    private static bool _mostrando;

    /// <summary>Se llama una vez al inicio, en <c>Program.Main</c>. <paramref name="mostrar"/> solo se cambia en las pruebas.</summary>
    public static void Instalar(Action<Exception>? mostrar = null)
    {
        _mostrar = mostrar ?? MostrarDialogo;
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        }
        catch (InvalidOperationException)
        {
            // Ya se crearon ventanas (pasa en las pruebas): el modo Automatic también usa ThreadException.
        }
        Application.ThreadException -= AlFallarEnInterfaz;
        Application.ThreadException += AlFallarEnInterfaz;
        TaskScheduler.UnobservedTaskException -= AlFallarTarea;
        TaskScheduler.UnobservedTaskException += AlFallarTarea;
    }

    /// <summary>Quita los manejadores (para las pruebas).</summary>
    public static void Desinstalar()
    {
        Application.ThreadException -= AlFallarEnInterfaz;
        TaskScheduler.UnobservedTaskException -= AlFallarTarea;
        _mostrar = null;
    }

    private static void AlFallarEnInterfaz(object? sender, ThreadExceptionEventArgs e)
    {
        // Si mientras se muestra un error se produce otro (por ejemplo en un temporizador), no se apilan diálogos.
        if (_mostrando) { Debug.WriteLine(e.Exception); return; }
        _mostrando = true;
        try { _mostrar?.Invoke(e.Exception); }
        finally { _mostrando = false; }
    }

    private static void AlFallarTarea(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine(e.Exception);
        e.SetObserved();
    }

    private static void MostrarDialogo(Exception ex)
    {
        if (!string.IsNullOrEmpty(Sesion.Bd.Usuario))
            _ = Sesion.Bd.RegistrarBitacoraAsync(Modulo.General, "Error no controlado", null, false, ex.ToString());
        var duenio = Form.ActiveForm ?? Application.OpenForms.Cast<Form>().LastOrDefault(f => f.Visible);
        Dialogos.Error(duenio, ex);
    }
}

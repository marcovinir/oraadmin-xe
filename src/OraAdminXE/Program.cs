using OraAdminXE.UI;

namespace OraAdminXE;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Un error que se escape de un evento muestra el diálogo de error en lugar de cerrar la app.
        ManejadorErrores.Instalar();

        using (var login = new FrmLogin())
        {
            if (login.ShowDialog() != DialogResult.OK)
                return;
        }

        Application.Run(new FrmPrincipal());
    }
}

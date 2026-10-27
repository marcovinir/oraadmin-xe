using OraAdminXE.UI;

namespace OraAdminXE;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using (var login = new FrmLogin())
        {
            if (login.ShowDialog() != DialogResult.OK)
                return;
        }

        Application.Run(new FrmPrincipal());
    }
}

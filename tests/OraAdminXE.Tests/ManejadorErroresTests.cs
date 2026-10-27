using OraAdminXE.UI;

namespace OraAdminXE.Tests;

/// <summary>Una excepción dentro de un evento async (async void) no debe cerrar la app.</summary>
public class ManejadorErroresTests
{
    [Fact]
    public void Excepcion_en_evento_async_llega_al_manejador_y_no_cierra_la_app()
    {
        Exception? recibida = null;
        Exception? fallaHilo = null;
        var hilo = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                ManejadorErrores.Instalar(ex => recibida = ex);

                using var ventana = new Form();
                var boton = new Button();
                boton.Click += async (_, _) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("falla de prueba");
                };
                ventana.Controls.Add(boton);
                ventana.Show();
                boton.PerformClick();

                var fin = DateTime.Now.AddSeconds(5);
                while (recibida is null && DateTime.Now < fin) { Application.DoEvents(); Thread.Sleep(10); }
                ventana.Close();
            }
            catch (Exception ex) { fallaHilo = ex; }
            finally { ManejadorErrores.Desinstalar(); }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        Assert.True(hilo.Join(15000));

        Assert.Null(fallaHilo);
        Assert.IsType<InvalidOperationException>(recibida);
        Assert.Equal("falla de prueba", recibida!.Message);
    }
}

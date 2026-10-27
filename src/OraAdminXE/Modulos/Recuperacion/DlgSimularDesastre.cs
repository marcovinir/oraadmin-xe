using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Recuperacion;

/// <summary>
/// Pide la tabla de DEMO_VENTAS que se va a borrar para la demostración (issue #14).
/// Así no hace falta salir a SQL Developer durante la presentación.
/// </summary>
public sealed class DlgSimularDesastre : VentanaBase
{
    private readonly ComboBox _cboTabla = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };

    public string Tabla => (string)_cboTabla.SelectedItem!;

    public DlgSimularDesastre(IEnumerable<(string Tabla, long Filas)> tablas)
    {
        Text = "Simular desastre";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var filas = tablas.ToDictionary(t => t.Tabla, t => t.Filas);
        _cboTabla.Items.AddRange(filas.Keys.ToArray<object>());
        _cboTabla.SelectedItem = filas.ContainsKey("DETALLE_FACTURA") ? "DETALLE_FACTURA" : filas.Keys.FirstOrDefault();
        var lblFilas = new Label { AutoSize = true, ForeColor = Tema.TextoSecundario, Margin = new Padding(0, 4, 0, 12) };
        void Mostrar() => lblFilas.Text = _cboTabla.SelectedItem is string t ? $"Hoy tiene {filas[t]:N0} filas." : "";
        _cboTabla.SelectedIndexChanged += (_, _) => Mostrar();
        Mostrar();

        var cuerpo = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(20, 18, 20, 4) };
        cuerpo.Controls.AddRange(
        [
            new Label { Text = "⚠  Esto es para la demostración", AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), ForeColor = Tema.Advertencia, Margin = new Padding(0, 0, 0, 8) },
            new Label { Text = $"Se borrará la tabla con DROP TABLE … PURGE (sin papelera).\r\nDespués se restaura desde un respaldo del esquema {SqlRecuperacion.EsquemaDemo}.", AutoSize = true, ForeColor = Tema.TextoSecundario, Margin = new Padding(0, 0, 0, 14) },
            new Label { Text = $"Tabla de {SqlRecuperacion.EsquemaDemo}", AutoSize = true, Margin = new Padding(0, 0, 0, 4) },
            _cboTabla,
            lblFilas
        ]);

        var btnBorrar = new BotonPlano { Text = "Continuar…", Estilo = EstiloBoton.Peligro, Icono = "", MinimumSize = new Size(120, 36), DialogResult = DialogResult.OK };
        var btnCancelar = new BotonPlano { Text = "Cancelar", MinimumSize = new Size(110, 36), DialogResult = DialogResult.Cancel };
        var pie = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(16, 8, 16, 16) };
        pie.Controls.AddRange([btnBorrar, btnCancelar]);

        var raiz = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        raiz.Controls.AddRange([cuerpo, pie]);
        pie.Width = cuerpo.PreferredSize.Width;
        Controls.Add(raiz);
        CancelButton = btnCancelar;
        btnBorrar.Enabled = _cboTabla.SelectedItem is not null;
    }
}

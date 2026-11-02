using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>Diálogo para otorgar a un usuario o rol: un rol, un privilegio de sistema o privilegios sobre una tabla.</summary>
public sealed class DlgOtorgar : VentanaBase
{
    private readonly string _beneficiario;
    private readonly Func<string, Task<string[]>> _tablasDe;
    private readonly RadioButton _rdoRol = new() { Text = "Rol", AutoSize = true, Checked = true };
    private readonly RadioButton _rdoSistema = new() { Text = "Privilegio de sistema", AutoSize = true };
    private readonly RadioButton _rdoObjeto = new() { Text = "Privilegio sobre una tabla", AutoSize = true };
    private readonly ComboBox _cboRol = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _cboSistema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _cboEsquema = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _cboTabla = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly CheckedListBox _lstPrivilegios = new() { Width = 320, Height = 90, CheckOnClick = true, MultiColumn = true, ColumnWidth = 105, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _chkOpcion = new() { AutoSize = true };
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro, MaximumSize = new Size(440, 0) };
    private readonly TableLayoutPanel _tabla = new() { ColumnCount = 2, AutoSize = true, Padding = new Padding(20, 8, 20, 4) };

    /// <summary>La sentencia GRANT que se armó al aceptar.</summary>
    public string Sql { get; private set; } = "";

    public DlgOtorgar(string beneficiario, IEnumerable<string> roles, IEnumerable<string> privilegiosSistema,
                      IEnumerable<string> esquemas, Func<string, Task<string[]>> tablasDe)
    {
        _beneficiario = beneficiario;
        _tablasDe = tablasDe;
        Text = $"Otorgar a {beneficiario}";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _cboRol.Items.AddRange(roles.Where(r => r != beneficiario).ToArray<object>());
        _cboSistema.Items.AddRange(DdlRoles.SistemaComunes.Concat(privilegiosSistema.Except(DdlRoles.SistemaComunes)).ToArray<object>());
        _cboEsquema.Items.AddRange(esquemas.ToArray<object>());
        _lstPrivilegios.Items.AddRange(DdlRoles.PrivilegiosObjeto.ToArray<object>());
        _lstPrivilegios.SetItemChecked(0, true);
        if (_cboRol.Items.Count > 0) _cboRol.SelectedIndex = 0;
        _cboSistema.SelectedIndex = 0;
        _cboEsquema.SelectedIndexChanged += async (_, _) => await CargarTablasAsync();
        if (_cboEsquema.Items.Contains("DEMO_VENTAS")) _cboEsquema.SelectedItem = "DEMO_VENTAS";

        var tipos = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(20, 18, 20, 4), WrapContents = false };
        tipos.Controls.AddRange([_rdoRol, _rdoSistema, _rdoObjeto]);
        foreach (var rdo in new[] { _rdoRol, _rdoSistema, _rdoObjeto })
        {
            rdo.Margin = new Padding(0, 0, 18, 0);
            rdo.CheckedChanged += (_, _) => { if (rdo.Checked) MostrarCampos(); };
        }

        _tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        _tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var btnAceptar = new BotonPlano { Text = "Otorgar", Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        btnAceptar.Click += (_, _) => Aceptar();
        var btnCancelar = new BotonPlano { Text = "Cancelar", MinimumSize = new Size(110, 36), DialogResult = DialogResult.Cancel };
        var pie = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Size = new Size(470, 64), Padding = new Padding(16, 8, 16, 16) };
        pie.Controls.AddRange([btnAceptar, btnCancelar]);

        var raiz = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        raiz.Controls.AddRange([tipos, _tabla, pie]);
        Controls.Add(raiz);
        AcceptButton = btnAceptar;
        CancelButton = btnCancelar;
        MostrarCampos();
    }

    private void MostrarCampos()
    {
        _tabla.SuspendLayout();
        _tabla.Controls.Clear();
        void Fila(string etiqueta, Control control) =>
            _tabla.Controls.AddRange([new Label { Text = etiqueta, AutoSize = true, Margin = new Padding(0, 6, 8, 10) }, control]);

        if (_rdoRol.Checked)
        {
            Fila("Rol", _cboRol);
            _chkOpcion.Text = "WITH ADMIN OPTION (podrá otorgarlo a otros)";
        }
        else if (_rdoSistema.Checked)
        {
            Fila("Privilegio", _cboSistema);
            _chkOpcion.Text = "WITH ADMIN OPTION (podrá otorgarlo a otros)";
        }
        else
        {
            Fila("Esquema", _cboEsquema);
            Fila("Tabla", _cboTabla);
            Fila("Privilegios", _lstPrivilegios);
            _chkOpcion.Text = "WITH GRANT OPTION (podrá otorgarlos a otros)";
        }
        _tabla.Controls.AddRange([new Label(), _chkOpcion, new Label(), _lblError]);
        _lblError.Text = "";
        _tabla.ResumeLayout();
    }

    private async Task CargarTablasAsync()
    {
        _cboTabla.Items.Clear();
        if (_cboEsquema.SelectedItem is not string esquema) return;
        try
        {
            _cboTabla.Items.AddRange((await _tablasDe(esquema)).ToArray<object>());
            if (_cboTabla.Items.Count > 0) _cboTabla.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _lblError.Text = ex.Message;
        }
    }

    private void Aceptar()
    {
        try
        {
            if (_rdoRol.Checked)
                Sql = DdlRoles.OtorgarRol(Requerido(_cboRol, "un rol"), _beneficiario, _chkOpcion.Checked);
            else if (_rdoSistema.Checked)
                Sql = DdlRoles.OtorgarSistema(Requerido(_cboSistema, "un privilegio"), _beneficiario, _chkOpcion.Checked);
            else
                Sql = DdlRoles.OtorgarObjeto(_lstPrivilegios.CheckedItems.Cast<string>(), Requerido(_cboEsquema, "un esquema"),
                    Requerido(_cboTabla, "una tabla"), _beneficiario, _chkOpcion.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
    }

    private static string Requerido(ComboBox combo, string que) =>
        combo.SelectedItem as string ?? throw new ArgumentException($"Elija {que}.");
}

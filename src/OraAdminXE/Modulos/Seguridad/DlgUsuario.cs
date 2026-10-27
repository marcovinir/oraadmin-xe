using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>Diálogo para crear un usuario, cambiar su contraseña o su tablespace y cuota.</summary>
public sealed class DlgUsuario : Form
{
    public enum Modo { Crear, Clave, Tablespace }

    private readonly Modo _modo;
    private readonly TextBox _txtUsuario = new() { CharacterCasing = CharacterCasing.Upper, Width = 300 };
    private readonly TextBox _txtClave = new() { UseSystemPasswordChar = true, Width = 300 };
    private readonly TextBox _txtConfirmar = new() { UseSystemPasswordChar = true, Width = 300 };
    private readonly ComboBox _cboTablespace = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _numCuota = new() { Minimum = 1, Maximum = 12288, Value = 50, Width = 100 };
    private readonly CheckBox _chkIlimitada = new() { Text = "Ilimitada", AutoSize = true, Margin = new Padding(12, 6, 0, 0) };
    private readonly ComboBox _cboPerfil = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly CheckBox _chkSesion = new() { Text = "Otorgar CREATE SESSION (para que pueda conectarse)", AutoSize = true, Checked = true };
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro };

    public string Usuario => _txtUsuario.Text.Trim();
    public string Clave => _txtClave.Text;
    public string Tablespace => (string)_cboTablespace.SelectedItem!;
    public int? CuotaMb => _chkIlimitada.Checked ? null : (int)_numCuota.Value;
    public string Perfil => (string)_cboPerfil.SelectedItem!;
    public bool OtorgarSesion => _chkSesion.Checked;

    public DlgUsuario(Modo modo, string? usuario, IEnumerable<string> tablespaces, IEnumerable<string> perfiles,
                      string? tablespaceActual = null)
    {
        _modo = modo;
        Text = modo switch { Modo.Crear => "Crear usuario", Modo.Clave => $"Cambiar contraseña de {usuario}", _ => $"Tablespace y cuota de {usuario}" };
        Font = Tema.Fuente;
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _cboTablespace.Items.AddRange(tablespaces.ToArray<object>());
        _cboTablespace.SelectedItem = tablespaceActual is not null && _cboTablespace.Items.Contains(tablespaceActual) ? tablespaceActual : "USERS";
        if (_cboTablespace.SelectedIndex < 0 && _cboTablespace.Items.Count > 0) _cboTablespace.SelectedIndex = 0;
        _cboPerfil.Items.AddRange(perfiles.ToArray<object>());
        _cboPerfil.SelectedItem = "DEFAULT";
        _chkIlimitada.CheckedChanged += (_, _) => _numCuota.Enabled = !_chkIlimitada.Checked;
        _txtUsuario.Text = usuario ?? "";

        var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(20, 20, 20, 8) };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        void Fila(string etiqueta, Control control) =>
            tabla.Controls.AddRange([new Label { Text = etiqueta, AutoSize = true, Margin = new Padding(0, 6, 16, 10) }, control]);

        if (modo == Modo.Crear)
            Fila("Usuario", _txtUsuario);
        if (modo is Modo.Crear or Modo.Clave)
        {
            Fila(modo == Modo.Crear ? "Contraseña" : "Nueva contraseña", _txtClave);
            Fila("Confirmar", _txtConfirmar);
        }
        if (modo is Modo.Crear or Modo.Tablespace)
        {
            Fila("Tablespace", _cboTablespace);
            var cuota = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
            cuota.Controls.AddRange([_numCuota, new Label { Text = "MB", AutoSize = true, Margin = new Padding(4, 6, 0, 0) }, _chkIlimitada]);
            Fila("Cuota", cuota);
        }
        if (modo == Modo.Crear)
        {
            Fila("Perfil", _cboPerfil);
            tabla.Controls.Add(new Label());
            tabla.Controls.Add(_chkSesion);
        }
        tabla.Controls.Add(new Label());
        tabla.Controls.Add(_lblError);

        var btnAceptar = new Button { Text = modo == Modo.Crear ? "Crear" : "Guardar", AutoSize = true, MinimumSize = new Size(110, 36), BackColor = Tema.Acento, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnAceptar.FlatAppearance.BorderSize = 0;
        var btnCancelar = new Button { Text = "Cancelar", AutoSize = true, MinimumSize = new Size(110, 36), DialogResult = DialogResult.Cancel };
        btnAceptar.Click += (_, _) => Aceptar();
        var pie = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(16, 8, 16, 16) };
        pie.Controls.AddRange([btnAceptar, btnCancelar]);

        var raiz = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        raiz.Controls.AddRange([tabla, pie]);
        pie.Width = tabla.PreferredSize.Width;
        Controls.Add(raiz);
        AcceptButton = btnAceptar;
        CancelButton = btnCancelar;
    }

    private void Aceptar()
    {
        var error =
            _modo == Modo.Crear && Usuario.Length == 0 ? "Escriba el nombre del usuario." :
            _modo != Modo.Tablespace && Clave.Length == 0 ? "Escriba la contraseña." :
            _modo != Modo.Tablespace && Clave != _txtConfirmar.Text ? "Las contraseñas no coinciden." :
            _modo != Modo.Tablespace && Clave.Contains('"') ? "La contraseña no puede contener comillas dobles." :
            _modo != Modo.Clave && _cboTablespace.SelectedItem is null ? "Elija un tablespace." :
            null;
        if (error is not null)
        {
            _lblError.Text = error;
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}

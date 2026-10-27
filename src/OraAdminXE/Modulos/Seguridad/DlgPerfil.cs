using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Seguridad;

/// <summary>
/// Diálogo para crear o editar un perfil (issue #10). Cada límite tiene su modo (valor, UNLIMITED o DEFAULT)
/// y su número; al editar solo se mandan en el ALTER PROFILE los límites que cambiaron.
/// </summary>
public sealed class DlgPerfil : VentanaBase
{
    private sealed record Fila(string Recurso, ComboBox Modo, NumericUpDown Numero, LimitePerfil Original);

    private readonly bool _crear;
    private readonly TextBox _txtNombre = new() { CharacterCasing = CharacterCasing.Upper, Width = 260 };
    private readonly List<Fila> _filas = [];
    private readonly Label _lblError = new() { AutoSize = true, ForeColor = Tema.Peligro };

    public string Perfil => _txtNombre.Text.Trim();

    /// <summary>Al crear: todos los límites que no quedaron en DEFAULT. Al editar: solo los que cambiaron.</summary>
    public IReadOnlyList<LimitePerfil> Limites { get; private set; } = [];

    /// <param name="actuales">null para crear; al editar, los límites actuales del perfil.</param>
    public DlgPerfil(string? perfil, IReadOnlyDictionary<string, LimitePerfil>? actuales)
    {
        _crear = actuales is null;
        Text = _crear ? "Crear perfil" : $"Editar perfil {perfil}";
        BackColor = Tema.Superficie;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var tabla = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Padding = new Padding(20, 18, 20, 4) };
        for (var i = 0; i < 4; i++) tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _txtNombre.Text = perfil ?? "PERFIL_DEMO";
        _txtNombre.Enabled = _crear;
        tabla.Controls.Add(new Label { Text = "Perfil", AutoSize = true, Font = Tema.FuenteNegrita, Margin = new Padding(0, 6, 16, 14) }, 0, 0);
        tabla.Controls.Add(_txtNombre, 1, 0);
        tabla.SetColumnSpan(_txtNombre, 3);

        // Valores de partida para un perfil nuevo: los de la demo (3 intentos y 1 minuto de bloqueo).
        var sugeridos = new Dictionary<string, LimitePerfil>
        {
            ["FAILED_LOGIN_ATTEMPTS"] = new("FAILED_LOGIN_ATTEMPTS", ModoLimite.Valor, 3),
            ["PASSWORD_LOCK_TIME"] = new("PASSWORD_LOCK_TIME", ModoLimite.Valor, 1),
            ["PASSWORD_LIFE_TIME"] = new("PASSWORD_LIFE_TIME", ModoLimite.Valor, 90),
            ["SESSIONS_PER_USER"] = new("SESSIONS_PER_USER", ModoLimite.Valor, 2),
            ["IDLE_TIME"] = new("IDLE_TIME", ModoLimite.Valor, 15)
        };
        var porDefecto = new Dictionary<string, decimal>
        {
            ["FAILED_LOGIN_ATTEMPTS"] = 3, ["PASSWORD_LOCK_TIME"] = 1, ["PASSWORD_LIFE_TIME"] = 90, ["PASSWORD_GRACE_TIME"] = 7,
            ["PASSWORD_REUSE_MAX"] = 5, ["SESSIONS_PER_USER"] = 2, ["IDLE_TIME"] = 15, ["CONNECT_TIME"] = 120
        };

        var fila = 1;
        foreach (var (recurso, unidad, descripcion) in DdlPerfiles.Editables)
        {
            var original = actuales?.GetValueOrDefault(recurso) ?? sugeridos.GetValueOrDefault(recurso) ?? new(recurso, ModoLimite.Default);
            var modo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Margin = new Padding(0, 2, 10, 6) };
            modo.Items.AddRange(["Valor", "UNLIMITED", "DEFAULT"]);
            modo.SelectedIndex = (int)original.Modo;
            var numero = new NumericUpDown
            {
                Minimum = 1, Maximum = 100000, Width = 90, Margin = new Padding(0, 2, 6, 6),
                Value = original.Modo == ModoLimite.Valor ? Math.Clamp(original.Numero, 1, 100000) : porDefecto[recurso],
                Enabled = original.Modo == ModoLimite.Valor
            };
            modo.SelectedIndexChanged += (_, _) => numero.Enabled = modo.SelectedIndex == 0;

            var etiqueta = new Label { Text = descripcion, AutoSize = true, Margin = new Padding(0, 0, 16, 0) };
            var nombre = new Label { Text = recurso, AutoSize = true, ForeColor = Tema.TextoSecundario, Font = Tema.FuentePequena, Margin = new Padding(0, 0, 16, 6) };
            var textos = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            textos.Controls.AddRange([etiqueta, nombre]);

            tabla.Controls.Add(textos, 0, fila);
            tabla.Controls.Add(modo, 1, fila);
            tabla.Controls.Add(numero, 2, fila);
            tabla.Controls.Add(new Label { Text = unidad, AutoSize = true, ForeColor = Tema.TextoSecundario, Margin = new Padding(0, 6, 0, 0) }, 3, fila);
            _filas.Add(new Fila(recurso, modo, numero, actuales?.GetValueOrDefault(recurso) ?? new(recurso, ModoLimite.Default)));
            fila++;
        }
        tabla.Controls.Add(new Label
        {
            Text = "DEFAULT = toma el valor del perfil DEFAULT.  PASSWORD_LOCK_TIME se guarda en días (minutos/1440).",
            AutoSize = true, ForeColor = Tema.TextoSecundario, Margin = new Padding(0, 8, 0, 4)
        }, 0, fila);
        tabla.SetColumnSpan(tabla.GetControlFromPosition(0, fila)!, 4);
        tabla.Controls.Add(_lblError, 0, fila + 1);
        tabla.SetColumnSpan(_lblError, 4);

        var btnAceptar = new BotonPlano { Text = _crear ? "Crear" : "Guardar", Estilo = EstiloBoton.Primario, MinimumSize = new Size(110, 36) };
        var btnCancelar = new BotonPlano { Text = "Cancelar", MinimumSize = new Size(110, 36), DialogResult = DialogResult.Cancel };
        btnAceptar.Click += (_, _) => Aceptar();
        var pie = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(16, 8, 16, 16) };
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
        if (Perfil.Length == 0)
        {
            _lblError.Text = "Escriba el nombre del perfil.";
            return;
        }
        var limites = _filas.Select(f => new LimitePerfil(f.Recurso, (ModoLimite)f.Modo.SelectedIndex, f.Numero.Value)).ToList();
        Limites = _crear
            ? limites.Where(l => l.Modo != ModoLimite.Default).ToList()
            : limites.Where(l => !Igual(l, _filas.First(f => f.Recurso == l.Recurso).Original)).ToList();
        if (Limites.Count == 0)
        {
            _lblError.Text = _crear ? "Defina al menos un límite (todos están en DEFAULT)." : "No cambió ningún límite.";
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool Igual(LimitePerfil a, LimitePerfil b) => a.Modo == b.Modo && (a.Modo != ModoLimite.Valor || a.Numero == b.Numero);
}

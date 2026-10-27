namespace OraAdminXE.Modulos.Seguridad;

partial class UcSeguridad
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent()
    {
        lblTitulo = new Label();
        lblDescripcion = new Label();
        tabSeguridad = new TabControl();
        tabUsuarios = new TabPage();
        ucUsuarios = new UcUsuarios();
        tabRoles = new TabPage();
        ucRoles = new UcRoles();
        tabPerfiles = new TabPage();
        lblPerfilesPendiente = new Label();
        tabSeguridad.SuspendLayout();
        tabUsuarios.SuspendLayout();
        tabRoles.SuspendLayout();
        tabPerfiles.SuspendLayout();
        SuspendLayout();
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16F);
        lblTitulo.ForeColor = Color.FromArgb(15, 23, 42);
        lblTitulo.Location = new Point(24, 20);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "Seguridad de usuarios";
        //
        // lblDescripcion
        //
        lblDescripcion.AutoSize = true;
        lblDescripcion.ForeColor = Color.FromArgb(100, 116, 139);
        lblDescripcion.Location = new Point(26, 58);
        lblDescripcion.Name = "lblDescripcion";
        lblDescripcion.Text = "Usuarios, roles, privilegios y perfiles";
        //
        // tabSeguridad
        //
        tabSeguridad.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tabSeguridad.Controls.Add(tabUsuarios);
        tabSeguridad.Controls.Add(tabRoles);
        tabSeguridad.Controls.Add(tabPerfiles);
        tabSeguridad.Location = new Point(24, 92);
        tabSeguridad.Name = "tabSeguridad";
        tabSeguridad.Padding = new Point(16, 6);
        tabSeguridad.SelectedIndex = 0;
        tabSeguridad.Size = new Size(1052, 584);
        tabSeguridad.TabIndex = 0;
        tabSeguridad.SelectedIndexChanged += TabSeguridad_SelectedIndexChanged;
        //
        // tabUsuarios
        //
        tabUsuarios.Controls.Add(ucUsuarios);
        tabUsuarios.Name = "tabUsuarios";
        tabUsuarios.Text = "Usuarios";
        tabUsuarios.UseVisualStyleBackColor = true;
        //
        // ucUsuarios
        //
        ucUsuarios.Dock = DockStyle.Fill;
        ucUsuarios.Name = "ucUsuarios";
        //
        // tabRoles
        //
        tabRoles.Controls.Add(ucRoles);
        tabRoles.Name = "tabRoles";
        tabRoles.Text = "Roles y privilegios";
        tabRoles.UseVisualStyleBackColor = true;
        //
        // ucRoles
        //
        ucRoles.Dock = DockStyle.Fill;
        ucRoles.Name = "ucRoles";
        //
        // tabPerfiles
        //
        tabPerfiles.Controls.Add(lblPerfilesPendiente);
        tabPerfiles.Name = "tabPerfiles";
        tabPerfiles.Text = "Perfiles";
        tabPerfiles.UseVisualStyleBackColor = true;
        //
        // lblPerfilesPendiente
        //
        lblPerfilesPendiente.AutoSize = true;
        lblPerfilesPendiente.ForeColor = Color.FromArgb(100, 116, 139);
        lblPerfilesPendiente.Location = new Point(20, 20);
        lblPerfilesPendiente.Name = "lblPerfilesPendiente";
        lblPerfilesPendiente.Text = "En construcción (issue #10). Cuando exista UcPerfiles, reemplazar esta etiqueta por: tabPerfiles.Controls.Add(new UcPerfiles { Dock = DockStyle.Fill })";
        //
        // UcSeguridad
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(lblTitulo);
        Controls.Add(lblDescripcion);
        Controls.Add(tabSeguridad);
        Name = "UcSeguridad";
        Size = new Size(1100, 700);
        tabSeguridad.ResumeLayout(false);
        tabUsuarios.ResumeLayout(false);
        tabRoles.ResumeLayout(false);
        tabPerfiles.ResumeLayout(false);
        tabPerfiles.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitulo;
    private Label lblDescripcion;
    private TabControl tabSeguridad;
    private TabPage tabUsuarios;
    private UcUsuarios ucUsuarios;
    private TabPage tabRoles;
    private UcRoles ucRoles;
    private TabPage tabPerfiles;
    private Label lblPerfilesPendiente;
}

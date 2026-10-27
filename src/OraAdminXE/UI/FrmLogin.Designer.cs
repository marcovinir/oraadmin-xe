namespace OraAdminXE.UI;

partial class FrmLogin
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

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        pnlEncabezado = new Panel();
        lblSubtitulo = new Label();
        lblTitulo = new Label();
        lblHost = new Label();
        txtHost = new TextBox();
        lblPuerto = new Label();
        numPuerto = new NumericUpDown();
        lblServicio = new Label();
        txtServicio = new TextBox();
        lblUsuario = new Label();
        txtUsuario = new TextBox();
        lblClave = new Label();
        txtClave = new TextBox();
        lblAyuda = new Label();
        lblEstado = new Label();
        btnConectar = new Button();
        btnSalir = new Button();
        pnlEncabezado.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numPuerto).BeginInit();
        SuspendLayout();
        //
        // pnlEncabezado
        //
        pnlEncabezado.BackColor = Color.FromArgb(30, 41, 59);
        pnlEncabezado.Controls.Add(lblSubtitulo);
        pnlEncabezado.Controls.Add(lblTitulo);
        pnlEncabezado.Dock = DockStyle.Top;
        pnlEncabezado.Location = new Point(0, 0);
        pnlEncabezado.Name = "pnlEncabezado";
        pnlEncabezado.Size = new Size(460, 112);
        pnlEncabezado.TabIndex = 0;
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 20F);
        lblTitulo.ForeColor = Color.FromArgb(239, 104, 84);
        lblTitulo.Location = new Point(36, 22);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "OraAdmin XE";
        //
        // lblSubtitulo
        //
        lblSubtitulo.AutoSize = true;
        lblSubtitulo.ForeColor = Color.FromArgb(203, 213, 225);
        lblSubtitulo.Location = new Point(40, 68);
        lblSubtitulo.Name = "lblSubtitulo";
        lblSubtitulo.Text = "Consola DBA para Oracle 21c XE";
        //
        // lblHost
        //
        lblHost.AutoSize = true;
        lblHost.Location = new Point(40, 136);
        lblHost.Name = "lblHost";
        lblHost.Text = "Servidor (host)";
        //
        // txtHost
        //
        txtHost.Location = new Point(40, 158);
        txtHost.Name = "txtHost";
        txtHost.Size = new Size(262, 25);
        txtHost.TabIndex = 1;
        //
        // lblPuerto
        //
        lblPuerto.AutoSize = true;
        lblPuerto.Location = new Point(318, 136);
        lblPuerto.Name = "lblPuerto";
        lblPuerto.Text = "Puerto";
        //
        // numPuerto
        //
        numPuerto.Location = new Point(318, 158);
        numPuerto.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        numPuerto.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numPuerto.Name = "numPuerto";
        numPuerto.Size = new Size(102, 25);
        numPuerto.TabIndex = 2;
        numPuerto.Value = new decimal(new int[] { 1521, 0, 0, 0 });
        //
        // lblServicio
        //
        lblServicio.AutoSize = true;
        lblServicio.Location = new Point(40, 198);
        lblServicio.Name = "lblServicio";
        lblServicio.Text = "Servicio (PDB)";
        //
        // txtServicio
        //
        txtServicio.Location = new Point(40, 220);
        txtServicio.Name = "txtServicio";
        txtServicio.Size = new Size(380, 25);
        txtServicio.TabIndex = 3;
        //
        // lblUsuario
        //
        lblUsuario.AutoSize = true;
        lblUsuario.Location = new Point(40, 260);
        lblUsuario.Name = "lblUsuario";
        lblUsuario.Text = "Usuario";
        //
        // txtUsuario
        //
        txtUsuario.CharacterCasing = CharacterCasing.Upper;
        txtUsuario.Location = new Point(40, 282);
        txtUsuario.Name = "txtUsuario";
        txtUsuario.Size = new Size(380, 25);
        txtUsuario.TabIndex = 4;
        //
        // lblClave
        //
        lblClave.AutoSize = true;
        lblClave.Location = new Point(40, 322);
        lblClave.Name = "lblClave";
        lblClave.Text = "Contraseña";
        //
        // txtClave
        //
        txtClave.Location = new Point(40, 344);
        txtClave.Name = "txtClave";
        txtClave.Size = new Size(380, 25);
        txtClave.TabIndex = 5;
        txtClave.UseSystemPasswordChar = true;
        //
        // lblAyuda
        //
        lblAyuda.AutoSize = true;
        lblAyuda.Font = new Font("Segoe UI", 9F);
        lblAyuda.ForeColor = Color.FromArgb(100, 116, 139);
        lblAyuda.Location = new Point(40, 380);
        lblAyuda.Name = "lblAyuda";
        lblAyuda.Text = "Conéctese a la PDB XEPDB1 con un usuario que tenga el rol DBA.";
        //
        // lblEstado
        //
        lblEstado.AutoSize = true;
        lblEstado.ForeColor = Color.FromArgb(100, 116, 139);
        lblEstado.Location = new Point(40, 408);
        lblEstado.Name = "lblEstado";
        lblEstado.Text = "";
        //
        // btnSalir
        //
        btnSalir.DialogResult = DialogResult.Cancel;
        btnSalir.Location = new Point(40, 440);
        btnSalir.Name = "btnSalir";
        btnSalir.Size = new Size(120, 40);
        btnSalir.TabIndex = 7;
        btnSalir.Text = "Salir";
        //
        // btnConectar
        //
        btnConectar.BackColor = Color.FromArgb(199, 70, 52);
        btnConectar.Cursor = Cursors.Hand;
        btnConectar.FlatAppearance.BorderSize = 0;
        btnConectar.FlatStyle = FlatStyle.Flat;
        btnConectar.Font = new Font("Segoe UI Semibold", 10.5F);
        btnConectar.ForeColor = Color.White;
        btnConectar.Location = new Point(220, 440);
        btnConectar.Name = "btnConectar";
        btnConectar.Size = new Size(200, 40);
        btnConectar.TabIndex = 6;
        btnConectar.Text = "Conectar";
        btnConectar.UseVisualStyleBackColor = false;
        btnConectar.Click += BtnConectar_Click;
        //
        // FrmLogin
        //
        AcceptButton = btnConectar;
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        CancelButton = btnSalir;
        ClientSize = new Size(460, 508);
        Controls.Add(pnlEncabezado);
        Controls.Add(lblHost);
        Controls.Add(txtHost);
        Controls.Add(lblPuerto);
        Controls.Add(numPuerto);
        Controls.Add(lblServicio);
        Controls.Add(txtServicio);
        Controls.Add(lblUsuario);
        Controls.Add(txtUsuario);
        Controls.Add(lblClave);
        Controls.Add(txtClave);
        Controls.Add(lblAyuda);
        Controls.Add(lblEstado);
        Controls.Add(btnConectar);
        Controls.Add(btnSalir);
        Font = new Font("Segoe UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "FrmLogin";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Conectar a Oracle — OraAdmin XE";
        pnlEncabezado.ResumeLayout(false);
        pnlEncabezado.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numPuerto).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Panel pnlEncabezado;
    private Label lblTitulo;
    private Label lblSubtitulo;
    private Label lblHost;
    private TextBox txtHost;
    private Label lblPuerto;
    private NumericUpDown numPuerto;
    private Label lblServicio;
    private TextBox txtServicio;
    private Label lblUsuario;
    private TextBox txtUsuario;
    private Label lblClave;
    private TextBox txtClave;
    private Label lblAyuda;
    private Label lblEstado;
    private Button btnConectar;
    private Button btnSalir;
}

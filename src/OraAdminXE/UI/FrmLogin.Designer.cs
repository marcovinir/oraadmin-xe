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
        lblTitulo = new Label();
        lblPendiente = new Label();
        btnEntrar = new Button();
        SuspendLayout();
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16F);
        lblTitulo.ForeColor = Color.FromArgb(199, 70, 52);
        lblTitulo.Location = new Point(32, 28);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "OraAdmin XE";
        //
        // lblPendiente
        //
        lblPendiente.Location = new Point(34, 80);
        lblPendiente.Name = "lblPendiente";
        lblPendiente.Size = new Size(330, 60);
        lblPendiente.Text = "Pantalla de login en construcción (issue «Núcleo: pantalla de login»).";
        //
        // btnEntrar
        //
        btnEntrar.Location = new Point(34, 150);
        btnEntrar.Name = "btnEntrar";
        btnEntrar.Size = new Size(160, 36);
        btnEntrar.Text = "Entrar sin conexión";
        btnEntrar.Click += BtnEntrar_Click;
        //
        // FrmLogin
        //
        AcceptButton = btnEntrar;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(400, 220);
        Controls.Add(lblTitulo);
        Controls.Add(lblPendiente);
        Controls.Add(btnEntrar);
        Font = new Font("Segoe UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "FrmLogin";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Conectar a Oracle";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitulo;
    private Label lblPendiente;
    private Button btnEntrar;
}

namespace OraAdminXE.Modulos.Performance;

partial class UcPerformance
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
        pestanas = new OraAdminXE.UI.Pestanas();
        SuspendLayout();
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16F);
        lblTitulo.ForeColor = Color.FromArgb(15, 23, 42);
        lblTitulo.Location = new Point(24, 20);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "Performance";
        //
        // lblDescripcion
        //
        lblDescripcion.AutoSize = true;
        lblDescripcion.ForeColor = Color.FromArgb(100, 116, 139);
        lblDescripcion.Location = new Point(26, 58);
        lblDescripcion.Name = "lblDescripcion";
        lblDescripcion.Text = "Sesiones, bloqueos, top SQL, esperas y métricas en vivo";
        //
        // pestanas
        //
        pestanas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pestanas.Location = new Point(24, 88);
        pestanas.Name = "pestanas";
        pestanas.Size = new Size(1052, 588);
        pestanas.TabIndex = 0;
        //
        // UcPerformance
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(lblTitulo);
        Controls.Add(lblDescripcion);
        Controls.Add(pestanas);
        Name = "UcPerformance";
        Size = new Size(1100, 700);
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitulo;
    private Label lblDescripcion;
    private OraAdminXE.UI.Pestanas pestanas;
}

namespace OraAdminXE.Modulos.Bitacora;

partial class UcBitacora
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
        pnlCuerpo = new Panel();
        SuspendLayout();
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16F);
        lblTitulo.ForeColor = Color.FromArgb(15, 23, 42);
        lblTitulo.Location = new Point(24, 20);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "Bitácora";
        //
        // lblDescripcion
        //
        lblDescripcion.AutoSize = true;
        lblDescripcion.ForeColor = Color.FromArgb(100, 116, 139);
        lblDescripcion.Location = new Point(26, 58);
        lblDescripcion.Name = "lblDescripcion";
        lblDescripcion.Text = "Historial de todo lo ejecutado desde la app, con su SQL y su resultado";
        //
        // pnlCuerpo (los filtros, la grilla y el detalle se agregan en el constructor de UcBitacora)
        //
        pnlCuerpo.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlCuerpo.Location = new Point(24, 88);
        pnlCuerpo.Name = "pnlCuerpo";
        pnlCuerpo.Size = new Size(1052, 588);
        pnlCuerpo.TabIndex = 0;
        //
        // UcBitacora
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(lblTitulo);
        Controls.Add(lblDescripcion);
        Controls.Add(pnlCuerpo);
        Name = "UcBitacora";
        Size = new Size(1100, 700);
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitulo;
    private Label lblDescripcion;
    private Panel pnlCuerpo;
}

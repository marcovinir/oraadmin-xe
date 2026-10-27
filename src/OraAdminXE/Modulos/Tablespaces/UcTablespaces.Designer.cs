namespace OraAdminXE.Modulos.Tablespaces;

partial class UcTablespaces
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
        btnRefrescar = new OraAdminXE.UI.BotonPlano();
        lblAvisoXe = new Label();
        lblTablespaces = new Label();
        lblArchivos = new Label();
        grdTablespaces = new DataGridView();
        grdArchivos = new DataGridView();
        ((System.ComponentModel.ISupportInitialize)grdTablespaces).BeginInit();
        ((System.ComponentModel.ISupportInitialize)grdArchivos).BeginInit();
        SuspendLayout();
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16F);
        lblTitulo.ForeColor = Color.FromArgb(15, 23, 42);
        lblTitulo.Location = new Point(24, 20);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "Tablespaces";
        //
        // lblDescripcion
        //
        lblDescripcion.AutoSize = true;
        lblDescripcion.ForeColor = Color.FromArgb(100, 116, 139);
        lblDescripcion.Location = new Point(26, 58);
        lblDescripcion.Name = "lblDescripcion";
        lblDescripcion.Text = "Uso actual y archivos físicos de tablespaces";
        //
        // btnRefrescar
        //
        btnRefrescar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefrescar.AutoSize = false;
        btnRefrescar.Icono = "\uE72C";
        btnRefrescar.Location = new Point(946, 24);
        btnRefrescar.Name = "btnRefrescar";
        btnRefrescar.Size = new Size(130, 36);
        btnRefrescar.TabIndex = 0;
        btnRefrescar.Text = "Refrescar";
        btnRefrescar.Click += BtnRefrescar_Click;
        //
        // lblAvisoXe
        //
        lblAvisoXe.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblAvisoXe.ForeColor = Color.FromArgb(100, 116, 139);
        lblAvisoXe.Location = new Point(24, 100);
        lblAvisoXe.Name = "lblAvisoXe";
        lblAvisoXe.Size = new Size(1052, 22);
        lblAvisoXe.Text = "Oracle Database XE admite hasta 12 GB de datos de usuario.";
        //
        // lblTablespaces
        //
        lblTablespaces.AutoSize = true;
        lblTablespaces.Font = new Font("Segoe UI Semibold", 12F);
        lblTablespaces.ForeColor = Color.FromArgb(15, 23, 42);
        lblTablespaces.Location = new Point(24, 138);
        lblTablespaces.Name = "lblTablespaces";
        lblTablespaces.Text = "Uso de tablespaces";
        //
        // grdTablespaces
        //
        grdTablespaces.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grdTablespaces.Location = new Point(24, 170);
        grdTablespaces.Name = "grdTablespaces";
        grdTablespaces.Size = new Size(1052, 230);
        grdTablespaces.TabIndex = 1;
        grdTablespaces.SelectionChanged += GrdTablespaces_SelectionChanged;
        //
        // lblArchivos
        //
        lblArchivos.AutoSize = true;
        lblArchivos.Font = new Font("Segoe UI Semibold", 12F);
        lblArchivos.ForeColor = Color.FromArgb(15, 23, 42);
        lblArchivos.Location = new Point(24, 416);
        lblArchivos.Name = "lblArchivos";
        lblArchivos.Text = "Datafiles y tempfiles del tablespace seleccionado";
        //
        // grdArchivos
        //
        grdArchivos.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        grdArchivos.Location = new Point(24, 448);
        grdArchivos.Name = "grdArchivos";
        grdArchivos.Size = new Size(1052, 228);
        grdArchivos.TabIndex = 2;
        //
        // UcTablespaces
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(lblTitulo);
        Controls.Add(lblDescripcion);
        Controls.Add(btnRefrescar);
        Controls.Add(lblAvisoXe);
        Controls.Add(lblTablespaces);
        Controls.Add(grdTablespaces);
        Controls.Add(lblArchivos);
        Controls.Add(grdArchivos);
        Name = "UcTablespaces";
        Size = new Size(1100, 700);
        ((System.ComponentModel.ISupportInitialize)grdTablespaces).EndInit();
        ((System.ComponentModel.ISupportInitialize)grdArchivos).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitulo;
    private Label lblDescripcion;
    private OraAdminXE.UI.BotonPlano btnRefrescar;
    private Label lblAvisoXe;
    private Label lblTablespaces;
    private Label lblArchivos;
    private DataGridView grdTablespaces;
    private DataGridView grdArchivos;
}

namespace OraAdminXE.Modulos.Dashboard;

partial class UcDashboard
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
        tlpTarjetas = new TableLayoutPanel();
        tarInstancia = new Tarjeta();
        tarActividad = new Tarjeta();
        tarSesiones = new Tarjeta();
        tarRespaldo = new Tarjeta();
        lblTablespaces = new Label();
        lblLimite = new Label();
        barLimite = new Panel();
        grdTablespaces = new DataGridView();
        tlpTarjetas.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)grdTablespaces).BeginInit();
        SuspendLayout();
        //
        // lblTitulo
        //
        lblTitulo.AutoSize = true;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16F);
        lblTitulo.ForeColor = Color.FromArgb(15, 23, 42);
        lblTitulo.Location = new Point(24, 20);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "Inicio";
        //
        // lblDescripcion
        //
        lblDescripcion.AutoSize = true;
        lblDescripcion.ForeColor = Color.FromArgb(100, 116, 139);
        lblDescripcion.Location = new Point(26, 58);
        lblDescripcion.Name = "lblDescripcion";
        lblDescripcion.Text = "Estado de la instancia, sesiones, tablespaces y último respaldo";
        //
        // btnRefrescar
        //
        btnRefrescar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefrescar.AutoSize = false;
        btnRefrescar.Icono = "";
        btnRefrescar.Location = new Point(946, 24);
        btnRefrescar.Name = "btnRefrescar";
        btnRefrescar.Size = new Size(130, 36);
        btnRefrescar.TabIndex = 0;
        btnRefrescar.Text = "Refrescar";
        btnRefrescar.Click += BtnRefrescar_Click;
        //
        // tlpTarjetas
        //
        tlpTarjetas.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        tlpTarjetas.ColumnCount = 4;
        tlpTarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpTarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpTarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpTarjetas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tlpTarjetas.Controls.Add(tarInstancia, 0, 0);
        tlpTarjetas.Controls.Add(tarActividad, 1, 0);
        tlpTarjetas.Controls.Add(tarSesiones, 2, 0);
        tlpTarjetas.Controls.Add(tarRespaldo, 3, 0);
        tlpTarjetas.Location = new Point(24, 92);
        tlpTarjetas.Name = "tlpTarjetas";
        tlpTarjetas.RowCount = 1;
        tlpTarjetas.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpTarjetas.Size = new Size(1064, 108);
        tlpTarjetas.TabIndex = 1;
        //
        // tarInstancia
        //
        tarInstancia.Dock = DockStyle.Fill;
        tarInstancia.Name = "tarInstancia";
        tarInstancia.Titulo = "Instancia";
        //
        // tarActividad
        //
        tarActividad.Dock = DockStyle.Fill;
        tarActividad.Name = "tarActividad";
        tarActividad.Titulo = "Tiempo arriba";
        //
        // tarSesiones
        //
        tarSesiones.Dock = DockStyle.Fill;
        tarSesiones.Name = "tarSesiones";
        tarSesiones.Titulo = "Sesiones de usuario";
        //
        // tarRespaldo
        //
        tarRespaldo.Dock = DockStyle.Fill;
        tarRespaldo.Name = "tarRespaldo";
        tarRespaldo.Titulo = "Último respaldo";
        //
        // lblTablespaces
        //
        lblTablespaces.AutoSize = true;
        lblTablespaces.Font = new Font("Segoe UI Semibold", 12F);
        lblTablespaces.ForeColor = Color.FromArgb(15, 23, 42);
        lblTablespaces.Location = new Point(24, 222);
        lblTablespaces.Name = "lblTablespaces";
        lblTablespaces.Text = "Uso de tablespaces";
        //
        // lblLimite
        //
        lblLimite.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblLimite.ForeColor = Color.FromArgb(100, 116, 139);
        lblLimite.Location = new Point(676, 218);
        lblLimite.Name = "lblLimite";
        lblLimite.Size = new Size(400, 20);
        lblLimite.Text = "Datos de usuario: —";
        lblLimite.TextAlign = ContentAlignment.MiddleRight;
        //
        // barLimite
        //
        barLimite.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        barLimite.Location = new Point(876, 240);
        barLimite.Name = "barLimite";
        barLimite.Size = new Size(200, 6);
        barLimite.Paint += BarLimite_Paint;
        //
        // grdTablespaces
        //
        grdTablespaces.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        grdTablespaces.Location = new Point(24, 260);
        grdTablespaces.Name = "grdTablespaces";
        grdTablespaces.Size = new Size(1052, 416);
        grdTablespaces.TabIndex = 2;
        grdTablespaces.CellPainting += GrdTablespaces_CellPainting;
        //
        // UcDashboard
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(lblTitulo);
        Controls.Add(lblDescripcion);
        Controls.Add(btnRefrescar);
        Controls.Add(tlpTarjetas);
        Controls.Add(lblTablespaces);
        Controls.Add(lblLimite);
        Controls.Add(barLimite);
        Controls.Add(grdTablespaces);
        Name = "UcDashboard";
        Size = new Size(1100, 700);
        tlpTarjetas.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)grdTablespaces).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitulo;
    private Label lblDescripcion;
    private OraAdminXE.UI.BotonPlano btnRefrescar;
    private TableLayoutPanel tlpTarjetas;
    private Tarjeta tarInstancia;
    private Tarjeta tarActividad;
    private Tarjeta tarSesiones;
    private Tarjeta tarRespaldo;
    private Label lblTablespaces;
    private Label lblLimite;
    private Panel barLimite;
    private DataGridView grdTablespaces;
}

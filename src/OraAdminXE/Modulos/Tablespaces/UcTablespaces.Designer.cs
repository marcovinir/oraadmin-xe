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
        pnlAcciones = new FlowLayoutPanel();
        btnCrear = new OraAdminXE.UI.BotonPlano();
        btnAgregarArchivo = new OraAdminXE.UI.BotonPlano();
        btnRedimensionar = new OraAdminXE.UI.BotonPlano();
        btnAutoextend = new OraAdminXE.UI.BotonPlano();
        btnOffline = new OraAdminXE.UI.BotonPlano();
        btnOnline = new OraAdminXE.UI.BotonPlano();
        btnSoloLectura = new OraAdminXE.UI.BotonPlano();
        btnLecturaEscritura = new OraAdminXE.UI.BotonPlano();
        btnEliminar = new OraAdminXE.UI.BotonPlano();
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
        // pnlAcciones
        //
        pnlAcciones.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlAcciones.Location = new Point(24, 92);
        pnlAcciones.Name = "pnlAcciones";
        pnlAcciones.Size = new Size(1052, 42);
        pnlAcciones.WrapContents = false;
        //
        // btnCrear
        //
        btnCrear.Estilo = OraAdminXE.UI.EstiloBoton.Primario;
        btnCrear.Icono = "\uE710";
        btnCrear.Text = "Crear";
        //
        // btnAgregarArchivo
        //
        btnAgregarArchivo.Icono = "\uE710";
        btnAgregarArchivo.Text = "Agregar datafile";
        //
        // btnRedimensionar
        //
        btnRedimensionar.Icono = "\uE8A7";
        btnRedimensionar.Text = "Redimensionar";
        //
        // btnAutoextend
        //
        btnAutoextend.Icono = "\uE895";
        btnAutoextend.Text = "Autoextend";
        //
        // btnOffline
        //
        btnOffline.Text = "Offline";
        //
        // btnOnline
        //
        btnOnline.Text = "Online";
        //
        // btnSoloLectura
        //
        btnSoloLectura.Text = "Solo lectura";
        //
        // btnLecturaEscritura
        //
        btnLecturaEscritura.Text = "Lectura/escritura";
        //
        // btnEliminar
        //
        btnEliminar.Estilo = OraAdminXE.UI.EstiloBoton.Peligro;
        btnEliminar.Icono = "\uE74D";
        btnEliminar.Text = "Eliminar";
        pnlAcciones.Controls.AddRange([btnCrear, btnAgregarArchivo, btnRedimensionar, btnAutoextend,
            btnOffline, btnOnline, btnSoloLectura, btnLecturaEscritura, btnEliminar]);
        //
        // lblAvisoXe
        //
        lblAvisoXe.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblAvisoXe.ForeColor = Color.FromArgb(100, 116, 139);
        lblAvisoXe.Location = new Point(24, 142);
        lblAvisoXe.Name = "lblAvisoXe";
        lblAvisoXe.Size = new Size(1052, 22);
        lblAvisoXe.Text = "Oracle Database XE admite hasta 12 GB de datos de usuario.";
        //
        // lblTablespaces
        //
        lblTablespaces.AutoSize = true;
        lblTablespaces.Font = new Font("Segoe UI Semibold", 12F);
        lblTablespaces.ForeColor = Color.FromArgb(15, 23, 42);
        lblTablespaces.Location = new Point(24, 180);
        lblTablespaces.Name = "lblTablespaces";
        lblTablespaces.Text = "Uso de tablespaces";
        //
        // grdTablespaces
        //
        grdTablespaces.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grdTablespaces.Location = new Point(24, 212);
        grdTablespaces.Name = "grdTablespaces";
        grdTablespaces.Size = new Size(1052, 188);
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
        Controls.Add(pnlAcciones);
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
    private FlowLayoutPanel pnlAcciones;
    private OraAdminXE.UI.BotonPlano btnCrear;
    private OraAdminXE.UI.BotonPlano btnAgregarArchivo;
    private OraAdminXE.UI.BotonPlano btnRedimensionar;
    private OraAdminXE.UI.BotonPlano btnAutoextend;
    private OraAdminXE.UI.BotonPlano btnOffline;
    private OraAdminXE.UI.BotonPlano btnOnline;
    private OraAdminXE.UI.BotonPlano btnSoloLectura;
    private OraAdminXE.UI.BotonPlano btnLecturaEscritura;
    private OraAdminXE.UI.BotonPlano btnEliminar;
    private Label lblAvisoXe;
    private Label lblTablespaces;
    private Label lblArchivos;
    private DataGridView grdTablespaces;
    private DataGridView grdArchivos;
}

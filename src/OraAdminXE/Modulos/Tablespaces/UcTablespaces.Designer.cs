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
        pnlAcciones = new TableLayoutPanel();
        flpAcciones = new FlowLayoutPanel();
        flpAccionesEstado = new FlowLayoutPanel();
        flpAccionesPeligro = new FlowLayoutPanel();
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
        lblTitulo.Font = OraAdminXE.UI.Tema.FuenteTitulo;
        lblTitulo.ForeColor = OraAdminXE.UI.Tema.Texto;
        lblTitulo.Location = new Point(24, 20);
        lblTitulo.Name = "lblTitulo";
        lblTitulo.Text = "Tablespaces";
        //
        // lblDescripcion
        //
        lblDescripcion.AutoSize = true;
        lblDescripcion.ForeColor = OraAdminXE.UI.Tema.TextoSecundario;
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
        pnlAcciones.ColumnCount = 2;
        pnlAcciones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlAcciones.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlAcciones.Location = new Point(24, 92);
        pnlAcciones.Name = "pnlAcciones";
        pnlAcciones.RowCount = 2;
        pnlAcciones.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        pnlAcciones.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        pnlAcciones.Size = new Size(1052, 92);
        //
        // flpAcciones
        //
        flpAcciones.Dock = DockStyle.Fill;
        flpAcciones.Name = "flpAcciones";
        flpAcciones.WrapContents = false;
        //
        // flpAccionesEstado
        //
        flpAccionesEstado.Dock = DockStyle.Fill;
        flpAccionesEstado.Name = "flpAccionesEstado";
        flpAccionesEstado.WrapContents = false;
        //
        // flpAccionesPeligro
        //
        flpAccionesPeligro.AutoSize = true;
        flpAccionesPeligro.Dock = DockStyle.Fill;
        flpAccionesPeligro.FlowDirection = FlowDirection.RightToLeft;
        flpAccionesPeligro.Name = "flpAccionesPeligro";
        flpAccionesPeligro.Padding = new Padding(0, 24, 0, 0);
        flpAccionesPeligro.WrapContents = false;
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
        flpAcciones.Controls.AddRange([btnCrear, btnAgregarArchivo, btnRedimensionar, btnAutoextend]);
        flpAccionesEstado.Controls.AddRange([btnOffline, btnOnline, btnSoloLectura, btnLecturaEscritura]);
        flpAccionesPeligro.Controls.Add(btnEliminar);
        pnlAcciones.Controls.Add(flpAcciones, 0, 0);
        pnlAcciones.Controls.Add(flpAccionesEstado, 0, 1);
        pnlAcciones.Controls.Add(flpAccionesPeligro, 1, 0);
        pnlAcciones.SetRowSpan(flpAccionesPeligro, 2);
        //
        // lblAvisoXe
        //
        lblAvisoXe.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblAvisoXe.ForeColor = OraAdminXE.UI.Tema.TextoSecundario;
        lblAvisoXe.Location = new Point(24, 192);
        lblAvisoXe.Name = "lblAvisoXe";
        lblAvisoXe.Size = new Size(1052, 22);
        lblAvisoXe.Text = "Oracle Database XE admite hasta 12 GB de datos de usuario.";
        //
        // lblTablespaces
        //
        lblTablespaces.AutoSize = true;
        lblTablespaces.Font = OraAdminXE.UI.Tema.FuenteNegrita;
        lblTablespaces.ForeColor = OraAdminXE.UI.Tema.Texto;
        lblTablespaces.Location = new Point(24, 230);
        lblTablespaces.Name = "lblTablespaces";
        lblTablespaces.Text = "Uso de tablespaces";
        //
        // grdTablespaces
        //
        grdTablespaces.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grdTablespaces.Location = new Point(24, 262);
        grdTablespaces.Name = "grdTablespaces";
        grdTablespaces.Size = new Size(1052, 188);
        grdTablespaces.TabIndex = 1;
        grdTablespaces.SelectionChanged += GrdTablespaces_SelectionChanged;
        //
        // lblArchivos
        //
        lblArchivos.AutoSize = true;
        lblArchivos.Font = OraAdminXE.UI.Tema.FuenteNegrita;
        lblArchivos.ForeColor = OraAdminXE.UI.Tema.Texto;
        lblArchivos.Location = new Point(24, 466);
        lblArchivos.Name = "lblArchivos";
        lblArchivos.Text = "Datafiles y tempfiles del tablespace seleccionado";
        //
        // grdArchivos
        //
        grdArchivos.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        grdArchivos.Location = new Point(24, 498);
        grdArchivos.Name = "grdArchivos";
        grdArchivos.Size = new Size(1052, 178);
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
    private TableLayoutPanel pnlAcciones;
    private FlowLayoutPanel flpAcciones;
    private FlowLayoutPanel flpAccionesEstado;
    private FlowLayoutPanel flpAccionesPeligro;
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

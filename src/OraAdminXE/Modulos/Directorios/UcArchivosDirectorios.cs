using System.Data;
using System.Diagnostics;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>Pestaña restringida a inspeccionar y administrar DMP catalogados en DIRECTORY activos verificados.</summary>
public sealed class UcArchivosDirectorios : UserControl
{
    private readonly ComboBox _cboDirectorios = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _huerfanos = new() { Dock = DockStyle.Fill };
    private readonly Label _lblResumen = new() { Dock = DockStyle.Top, Height = 30, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lblDetalle = new() { Dock = DockStyle.Bottom, Height = 52, ForeColor = Tema.TextoSecundario, Padding = new Padding(0, 6, 0, 0) };
    private readonly Label _lblHuerfanos = new() { Dock = DockStyle.Top, Height = 28, Font = Tema.FuenteNegrita, ForeColor = Tema.TextoSecundario, Text = "Respaldos COMPLETADO sin DMP (solo tras una inspección correcta)" };
    private readonly BotonPlano _btnLog = Tema.Boton("Ver log", primario: true, icono: "▤");
    private readonly BotonPlano _btnRenombrar = Tema.Boton("Renombrar DMP", icono: "✎");
    private readonly BotonPlano _btnEliminar = Tema.Boton("Eliminar DMP", icono: "✕");
    private readonly BotonPlano _btnAbrir = Tema.Boton("Abrir carpeta", icono: "▣");
    private readonly BotonPlano _btnMarcar = Tema.Boton("Marcar como ELIMINADO", icono: "⚠");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "↻");
    private readonly List<ArchivoListado> _archivos = [];
    private readonly List<RespaldoAusente> _respaldosAusentes = [];
    private EstadoInspeccionDirectorio _estadoInspeccion = EstadoInspeccionDirectorio.ErrorIo;
    private string? _rutaVerificada;
    private bool _ocupado;
    private bool _cargando;

    private static IBaseDatos Bd => Sesion.Bd;

    public UcArchivosDirectorios()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnEliminar.Estilo = EstiloBoton.Peligro;
        _btnMarcar.Estilo = EstiloBoton.Peligro;

        var selector = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        selector.Controls.AddRange([new Label { Text = "DIRECTORY activo:", AutoSize = true, Margin = new Padding(0, 10, 8, 0) }, _cboDirectorios]);
        var acciones = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        acciones.Controls.AddRange([_btnLog, _btnRenombrar, _btnAbrir]);
        var accionesDerecha = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        accionesDerecha.Controls.AddRange([_btnEliminar, _btnMarcar, _btnRefrescar]);
        var barra = new TableLayoutPanel { Dock = DockStyle.Top, Height = 94, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 0, 0, 4) };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        barra.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        barra.Controls.Add(selector, 0, 0);
        barra.SetColumnSpan(selector, 2);
        barra.Controls.Add(acciones, 0, 1);
        barra.Controls.Add(accionesDerecha, 1, 1);

        ConfigurarGrilla(_grilla, "No hay .dmp ni .log en el DIRECTORY seleccionado.");
        ConfigurarGrilla(_huerfanos, "No hay respaldos COMPLETADO con DMP ausente.");
        Tema.ColumnaEstado(_grilla, "ESTADO");
        Tema.ColumnaEstado(_grilla, "ASOCIACION", t => t == "CATALOGADO" ? TipoEstado.Ok : TipoEstado.Advertencia);
        Tema.ColumnaEstado(_huerfanos, "ESTADO");

        var panelArchivos = new Panel { Dock = DockStyle.Fill };
        panelArchivos.Controls.Add(_grilla);
        panelArchivos.Controls.Add(_lblDetalle);
        panelArchivos.Controls.Add(_lblResumen);
        var panelHuerfanos = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        panelHuerfanos.Controls.Add(_huerfanos);
        panelHuerfanos.Controls.Add(_lblHuerfanos);
        var division = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 310, Panel1MinSize = 180, Panel2MinSize = 110, BackColor = Tema.Borde };
        division.Panel1.Controls.Add(panelArchivos);
        division.Panel2.Controls.Add(panelHuerfanos);

        Controls.Add(division);
        Controls.Add(barra);
        _cboDirectorios.SelectedIndexChanged += async (_, _) => await CargarArchivosAsync();
        _grilla.SelectionChanged += (_, _) => ActualizarDetalleYBotones();
        _huerfanos.SelectionChanged += (_, _) => ActualizarBotones();
        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnLog.Click += async (_, _) => await VerLogAsync();
        _btnRenombrar.Click += async (_, _) => await RenombrarAsync();
        _btnEliminar.Click += async (_, _) => await EliminarAsync();
        _btnAbrir.Click += async (_, _) => await AbrirCarpetaAsync();
        _btnMarcar.Click += async (_, _) => await MarcarAusenteAsync();
        ActualizarBotones();
    }

    private void ConfigurarGrilla(DataGridView grilla, string vacio)
    {
        Tema.AplicarGrilla(grilla);
        grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grilla.MultiSelect = false;
        grilla.RowTemplate.Height = 34;
        grilla.ShowCellToolTips = true;
        Tema.MensajeVacio(grilla, vacio, "▤");
    }

    private DirectorioActivo? DirectorioSeleccionado => _cboDirectorios.SelectedItem as DirectorioActivo;
    private ArchivoListado? ArchivoSeleccionado => _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].Cells["NOMBRE"].Value is string nombre
        ? _archivos.FirstOrDefault(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase)) : null;
    private RespaldoAusente? AusenteSeleccionado => _huerfanos.SelectedRows.Count > 0 && _huerfanos.SelectedRows[0].Cells["ID_RESPALDO"].Value is int id
        ? _respaldosAusentes.FirstOrDefault(a => a.IdRespaldo == id) : null;

    public async Task CargarAsync()
    {
        if (string.IsNullOrEmpty(Bd.Usuario) || _ocupado) return;
        var anterior = DirectorioSeleccionado?.Id;
        _cargando = true;
        ActualizarEstadoInteraccion();
        try
        {
            var tabla = await Bd.ConsultarAsync(SqlDirectorios.DirectoriosActivosArchivos);
            _cboDirectorios.Items.Clear();
            foreach (DataRow f in tabla.Rows)
                _cboDirectorios.Items.Add(new DirectorioActivo(Convert.ToInt32(f["ID_DIRECTORIO"]), (string)f["NOMBRE_ORACLE"], (string)f["RUTA_CATALOGO"], f["DIRECTORY_PATH"] as string ?? ""));
            _cboDirectorios.SelectedItem = _cboDirectorios.Items.Cast<DirectorioActivo>().FirstOrDefault(d => d.Id == anterior)
                                          ?? _cboDirectorios.Items.Cast<DirectorioActivo>().FirstOrDefault();
            if (_cboDirectorios.SelectedItem is null) Limpiar("No hay DIRECTORY activos que existan en Oracle.");
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        finally { _cargando = false; ActualizarEstadoInteraccion(); }
        if (DirectorioSeleccionado is not null) await CargarArchivosAsync();
    }

    private async Task CargarArchivosAsync()
    {
        if (_cargando || _ocupado || DirectorioSeleccionado is not { } directorio) return;
        _cargando = true;
        ActualizarEstadoInteraccion();
        Tema.Cargando(_grilla, true);
        try
        {
            var catalogo = await Bd.ConsultarAsync(SqlDirectorios.CatalogoArchivosDeDirectorio, Parametro.De("id_directorio", directorio.Id));
            Inspeccionar(directorio, catalogo);
        }
        catch (Exception ex)
        {
            Limpiar("No se pudo consultar el catálogo del DIRECTORY.");
            Dialogos.Error(this, ex);
        }
        finally
        {
            _cargando = false;
            Tema.Cargando(_grilla, false);
            ActualizarDetalleYBotones();
            ActualizarEstadoInteraccion();
        }
    }

    private void Inspeccionar(DirectorioActivo directorio, DataTable catalogo)
    {
        _archivos.Clear();
        _respaldosAusentes.Clear();
        _rutaVerificada = null;
        try
        {
            if (string.IsNullOrWhiteSpace(directorio.RutaOracle))
                throw new ArgumentException("El objeto DIRECTORY ya no existe en Oracle.");
            if (!ArchivosDirectorios.RutasCoinciden(directorio.RutaCatalogo, directorio.RutaOracle))
                throw new ArgumentException("La ruta del catálogo no coincide con la ruta vigente del DIRECTORY en Oracle.");
            _rutaVerificada = ArchivosDirectorios.ValidarRutaLectura(directorio.RutaCatalogo);
            if (!Directory.Exists(_rutaVerificada)) { _estadoInspeccion = EstadoInspeccionDirectorio.Inexistente; Limpiar("La carpeta del DIRECTORY no existe para la cuenta de Windows actual."); return; }

            var asociaciones = catalogo.AsEnumerable().Select(f => AsociacionDesde(f)).ToList();
            foreach (var archivo in new DirectoryInfo(_rutaVerificada).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                if ((archivo.Attributes & FileAttributes.ReparsePoint) != 0 || !ArchivosDirectorios.EsExtensionPermitida(archivo.Name)) continue;
                var asociacion = asociaciones.FirstOrDefault(a => ArchivosDirectorios.CoincideNombre(a.Archivo, archivo.Name) &&
                    (ArchivosDirectorios.EsDump(archivo.Name) ? a.Tipo == "DMP" : a.Tipo.StartsWith("LOG_", StringComparison.Ordinal)));
                _archivos.Add(new ArchivoListado(archivo.Name, archivo.Length, archivo.LastWriteTime, asociacion));
            }
            _archivos.Sort((a, b) => string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase));
            var presentes = _archivos.Where(a => ArchivosDirectorios.EsDump(a.Nombre)).Select(a => a.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var a in asociaciones.Where(a => a.Tipo == "DMP" && string.Equals(a.Estado, "COMPLETADO", StringComparison.OrdinalIgnoreCase) && !presentes.Contains(a.Archivo)))
                _respaldosAusentes.Add(new RespaldoAusente(a.IdRespaldo, directorio.Id, a.Archivo, a.Job, a.Estado));
            _estadoInspeccion = EstadoInspeccionDirectorio.Correcto;
            CargarTablas();
            var mb = _archivos.Sum(a => a.Bytes) / 1048576d;
            _lblResumen.Text = $"{_archivos.Count} archivo(s) · {mb:0.##} MB · Directorio verificado: {_rutaVerificada} · UTL_FILE confirmará permisos de Oracle al mutar.";
        }
        catch (UnauthorizedAccessException) { _estadoInspeccion = EstadoInspeccionDirectorio.AccesoDenegado; Limpiar("Acceso denegado: no se pueden inspeccionar archivos ni determinar huérfanos."); }
        catch (ArgumentException ex) { _estadoInspeccion = EstadoInspeccionDirectorio.Inseguro; Limpiar("DIRECTORY no seguro: " + ex.Message); }
        catch (IOException ex) { _estadoInspeccion = EstadoInspeccionDirectorio.ErrorIo; Limpiar("No se pudo inspeccionar el DIRECTORY: " + ex.Message); }
    }

    private static AsociacionArchivo AsociacionDesde(DataRow fila) => new(
        Convert.ToInt32(fila["ID_RESPALDO"]), (string)fila["ARCHIVO"], (string)fila["TIPO"], (string)fila["NOMBRE_JOB"], (string)fila["ESTADO"], Convert.ToInt32(fila["RESTAURACION_ACTIVA"]) != 0);

    private void CargarTablas()
    {
        var archivos = new DataTable();
        archivos.Columns.Add("NOMBRE"); archivos.Columns.Add("TIPO"); archivos.Columns.Add("MB", typeof(double)); archivos.Columns.Add("MODIFICADO", typeof(DateTime));
        archivos.Columns.Add("ASOCIACION"); archivos.Columns.Add("ESTADO"); archivos.Columns.Add("ADVERTENCIA");
        foreach (var a in _archivos)
            archivos.Rows.Add(a.Nombre, ArchivosDirectorios.EsDump(a.Nombre) ? "DMP" : "LOG", Math.Round(a.Bytes / 1048576d, 2), a.Modificado,
                a.Asociacion is null ? "SIN CATÁLOGO" : "CATALOGADO", a.Asociacion?.Estado ?? "—", a.Asociacion is null ? "Archivo desconocido: solo lectura" : a.Asociacion.Tipo == "LOG_RESTAURACION" ? "Log de restauración: solo lectura" : "");
        _grilla.DataSource = archivos;
        Encabezados(_grilla, ["NOMBRE", "TIPO", "MB", "MODIFICADO", "ASOCIACION", "ESTADO", "ADVERTENCIA"]);

        var ausentes = new DataTable();
        ausentes.Columns.Add("ID_RESPALDO", typeof(int)); ausentes.Columns.Add("ARCHIVO_DUMP"); ausentes.Columns.Add("NOMBRE_JOB"); ausentes.Columns.Add("ESTADO");
        foreach (var r in _respaldosAusentes) ausentes.Rows.Add(r.IdRespaldo, r.Archivo, r.Job, r.Estado);
        _huerfanos.DataSource = ausentes;
        Encabezados(_huerfanos, ["ID_RESPALDO", "ARCHIVO_DUMP", "NOMBRE_JOB", "ESTADO"]);
    }

    private void Limpiar(string mensaje)
    {
        _archivos.Clear(); _respaldosAusentes.Clear(); _rutaVerificada = null;
        _grilla.DataSource = null; _huerfanos.DataSource = null;
        _lblResumen.Text = mensaje; _lblDetalle.Text = "";
    }

    private static void Encabezados(DataGridView grilla, IReadOnlyList<string> orden)
    {
        foreach (DataGridViewColumn col in grilla.Columns)
        {
            col.Visible = orden.Contains(col.Name);
            col.HeaderText = col.Name switch { "MB" => "Tamaño MB", "MODIFICADO" => "Modificado", "ASOCIACION" => "Catálogo", "ADVERTENCIA" => "Aviso", "ARCHIVO_DUMP" => "DMP esperado", "NOMBRE_JOB" => "Job", _ => col.Name.Replace('_', ' ') };
            col.Width = col.Name switch
            {
                "NOMBRE" or "ARCHIVO_DUMP" => 340,
                "ADVERTENCIA" => 280,
                "NOMBRE_JOB" => 220,
                "MODIFICADO" => 135,
                "ASOCIACION" => 115,
                "ESTADO" => 120,
                "ID_RESPALDO" => 105,
                "TIPO" => 75,
                "MB" => 95,
                _ => 130
            };
            if (col.Name == "MODIFICADO") col.DefaultCellStyle.Format = "dd/MM/yy HH:mm";
            if (col.Name == "MB") { col.DefaultCellStyle.Format = "0.00"; col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
        }
        for (var i = 0; i < orden.Count; i++) grilla.Columns[orden[i]]!.DisplayIndex = i;
        grilla.CellFormatting -= FormatearCelda;
        grilla.CellFormatting += FormatearCelda;
    }

    private static void FormatearCelda(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grilla || e.RowIndex < 0 || e.ColumnIndex < 0 || e.Value is not string texto) return;
        var columna = grilla.Columns[e.ColumnIndex].Name;
        if (columna is "NOMBRE" or "ADVERTENCIA" or "ARCHIVO_DUMP" or "NOMBRE_JOB")
            grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = texto;
    }

    private void ActualizarDetalleYBotones()
    {
        var a = ArchivoSeleccionado;
        _lblDetalle.Text = a?.Asociacion is { } c ? $"Respaldo #{c.IdRespaldo} · job {c.Job} · {c.Estado}" : a is null ? "" : "Archivo no asociado: no se puede modificar.";
        ActualizarBotones();
    }

    private void ActualizarBotones()
    {
        var sesion = !string.IsNullOrEmpty(Bd.Usuario);
        var ocupado = _ocupado || _cargando;
        var correcto = _estadoInspeccion == EstadoInspeccionDirectorio.Correcto && _rutaVerificada is not null;
        var a = ArchivoSeleccionado;
        var dumpMutable = DirectorioSeleccionado is { } directorio && ArchivosDirectorios.EsDirectorioGestionado(directorio.Nombre) &&
                          a?.Asociacion is { Tipo: "DMP", Estado: "COMPLETADO" } && ArchivosDirectorios.EsDump(a.Nombre);
        _cboDirectorios.Enabled = !ocupado && sesion;
        _btnRefrescar.Enabled = !ocupado && sesion;
        _btnAbrir.Enabled = !ocupado && correcto;
        _btnLog.Enabled = !ocupado && correcto && a is not null && ArchivosDirectorios.EsLog(a.Nombre) && a.Asociacion is not null;
        _btnRenombrar.Enabled = _btnEliminar.Enabled = !ocupado && correcto && dumpMutable;
        _btnMarcar.Enabled = !ocupado && correcto && AusenteSeleccionado is not null;
    }

    private async Task VerLogAsync()
    {
        if (ArchivoSeleccionado is not { } archivo || !ArchivosDirectorios.EsLog(archivo.Nombre)) return;
        try
        {
            var ruta = await RevalidarLogAsync(archivo);
            var log = await ArchivosDirectorios.LeerLogLimitadoAsync(ruta);
            using var dlg = new DlgLogArchivoDirectorio(archivo.Nombre, log);
            dlg.ShowDialog(this);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task AbrirCarpetaAsync()
    {
        try
        {
            var ruta = await RevalidarDirectorioAsync();
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{ruta}\"", UseShellExecute = true });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task RenombrarAsync()
    {
        if (ArchivoSeleccionado is not { } archivo) return;
        using var dlg = new DlgRenombrarDump(archivo.Nombre);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        string destino;
        try { destino = ArchivosDirectorios.ValidarNombreDump(dlg.NombreNuevo); }
        catch (Exception ex) { Dialogos.Error(this, ex); return; }
        if (string.Equals(destino, archivo.Nombre, StringComparison.OrdinalIgnoreCase)) return;
        await EjecutarMutacionAsync(archivo, "Renombrar DMP", async contexto =>
        {
            if (File.Exists(Path.Combine(contexto.Ruta, destino))) throw new ArgumentException("Ya existe un archivo con ese nombre en el DIRECTORY.");
            var vista = $"-- DIRECTORY: {contexto.Directorio}\r\n-- {archivo.Nombre} → {destino}\r\n{SqlDirectorios.RenombrarArchivoFisico.Trim()}\r\n{SqlDirectorios.RenombrarDumpCatalogo.Trim()}";
            if (!Dialogos.ConfirmarSql(this, "Renombrar DMP", vista)) return;
            contexto = await RevalidarOperacionAsync(archivo);
            if (File.Exists(Path.Combine(contexto.Ruta, destino))) throw new ArgumentException("El nombre destino apareció durante la confirmación.");
            await Bd.EjecutarAsync(Modulo.Directorios, "Renombrar DMP físicamente", SqlDirectorios.RenombrarArchivoFisico,
                Parametro.De("dir_origen", contexto.Directorio), Parametro.De("archivo_origen", archivo.Nombre), Parametro.De("dir_destino", contexto.Directorio), Parametro.De("archivo_destino", destino));
            try
            {
                var filas = await Bd.EjecutarAsync(Modulo.Directorios, "Actualizar catálogo tras renombrar DMP", SqlDirectorios.RenombrarDumpCatalogo,
                    Parametro.De("archivo_destino", destino), Parametro.De("id_respaldo", contexto.IdRespaldo), Parametro.De("id_directorio", contexto.IdDirectorio), Parametro.De("archivo_origen", archivo.Nombre));
                if (filas != 1) throw new InvalidOperationException("El archivo se renombró físicamente, pero el catálogo cambió y no se actualizó. No se intentó revertir.");
                Dialogos.Exito(this, $"{archivo.Nombre} se renombró a {destino}.");
            }
            catch (Exception ex)
            {
                Toast.Mostrar(this, "Archivo renombrado físicamente; catálogo pendiente de reconciliación.", TipoEstado.Advertencia);
                throw new InvalidOperationException("El renombrado físico terminó, pero falló la actualización del catálogo. Refresque y reconcilie manualmente.", ex);
            }
        });
    }

    private async Task EliminarAsync()
    {
        if (ArchivoSeleccionado is not { } archivo) return;
        await EjecutarMutacionAsync(archivo, "Eliminar DMP", async contexto =>
        {
            var vista = $"-- Recuperar desde este respaldo puede dejar de ser posible.\r\n-- DIRECTORY: {contexto.Directorio}; archivo: {archivo.Nombre}\r\n{SqlDirectorios.EliminarArchivoFisico.Trim()}\r\n{SqlDirectorios.MarcarRespaldoEliminado.Trim()}";
            if (!Dialogos.ConfirmarSql(this, "Eliminar DMP", vista, destructiva: true)) return;
            contexto = await RevalidarOperacionAsync(archivo);
            await Bd.EjecutarAsync(Modulo.Directorios, "Eliminar DMP físicamente", SqlDirectorios.EliminarArchivoFisico,
                Parametro.De("directorio", contexto.Directorio), Parametro.De("archivo", archivo.Nombre));
            try
            {
                var filas = await Bd.EjecutarAsync(Modulo.Directorios, "Marcar respaldo ELIMINADO", SqlDirectorios.MarcarRespaldoEliminado,
                    Parametro.De("observacion", "DMP eliminado desde Directorios"), Parametro.De("id_respaldo", contexto.IdRespaldo), Parametro.De("id_directorio", contexto.IdDirectorio), Parametro.De("archivo_dump", archivo.Nombre));
                if (filas != 1) throw new InvalidOperationException("El DMP se eliminó físicamente, pero el catálogo cambió y no se actualizó.");
                Dialogos.Exito(this, $"{archivo.Nombre} se eliminó y el respaldo quedó ELIMINADO.");
            }
            catch (Exception ex)
            {
                Toast.Mostrar(this, "Archivo eliminado físicamente; catálogo pendiente de reconciliación.", TipoEstado.Advertencia);
                throw new InvalidOperationException("El archivo se eliminó físicamente, pero falló el cambio de catálogo. Refresque y reconcilie manualmente.", ex);
            }
        });
    }

    private async Task MarcarAusenteAsync()
    {
        if (AusenteSeleccionado is not { } ausente) return;
        await EjecutarOcupadoAsync(async () =>
        {
            var contexto = await RevalidarOperacionAsync(new ArchivoListado(ausente.Archivo, 0, DateTime.MinValue, new AsociacionArchivo(ausente.IdRespaldo, ausente.Archivo, "DMP", ausente.Job, ausente.Estado, false)), exigirArchivo: false);
            var ruta = Path.Combine(contexto.Ruta, ausente.Archivo);
            if (File.Exists(ruta)) throw new ArgumentException("El DMP volvió a aparecer; no se modificó el catálogo.");
            var vista = $"-- Se conserva el historial; solo cambia el estado del respaldo.\r\n-- DMP ausente confirmado: {ausente.Archivo}\r\n{SqlDirectorios.MarcarRespaldoEliminado.Trim()}";
            if (!Dialogos.ConfirmarSql(this, "Marcar respaldo como ELIMINADO", vista, destructiva: true)) return;
            contexto = await RevalidarOperacionAsync(new ArchivoListado(ausente.Archivo, 0, DateTime.MinValue, new AsociacionArchivo(ausente.IdRespaldo, ausente.Archivo, "DMP", ausente.Job, ausente.Estado, false)), exigirArchivo: false);
            if (File.Exists(Path.Combine(contexto.Ruta, ausente.Archivo))) throw new ArgumentException("El DMP volvió a aparecer durante la confirmación; no se modificó el catálogo.");
            var filas = await Bd.EjecutarAsync(Modulo.Directorios, "Reconciliar respaldo sin DMP", SqlDirectorios.MarcarRespaldoEliminado,
                Parametro.De("observacion", "DMP ausente confirmado y reconciliado desde Directorios"), Parametro.De("id_respaldo", contexto.IdRespaldo), Parametro.De("id_directorio", contexto.IdDirectorio), Parametro.De("archivo_dump", ausente.Archivo));
            if (filas != 1) throw new InvalidOperationException("El catálogo cambió mientras se confirmaba la reconciliación. Refresque e intente de nuevo.");
            Dialogos.Exito(this, $"Respaldo #{ausente.IdRespaldo} marcado como ELIMINADO.");
        });
    }

    private async Task EjecutarMutacionAsync(ArchivoListado archivo, string accion, Func<ContextoOperacion, Task> ejecutar) =>
        await EjecutarOcupadoAsync(async () => await ejecutar(await RevalidarOperacionAsync(archivo)));

    private async Task EjecutarOcupadoAsync(Func<Task> accion)
    {
        if (_ocupado) return;
        _ocupado = true;
        ActualizarEstadoInteraccion();
        try { await accion(); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        finally { _ocupado = false; await CargarArchivosAsync(); ActualizarEstadoInteraccion(); }
    }

    private void ActualizarEstadoInteraccion()
    {
        UseWaitCursor = _ocupado || _cargando;
        ActualizarBotones();
    }

    private async Task<ContextoOperacion> RevalidarOperacionAsync(ArchivoListado archivo, bool exigirArchivo = true)
    {
        if (archivo.Asociacion is not { Tipo: "DMP" } asociacion || !ArchivosDirectorios.EsBasenameSeguro(archivo.Nombre, ".dmp"))
            throw new ArgumentException("Solo se pueden administrar DMP asociados exactamente al catálogo.");
        var directorio = DirectorioSeleccionado ?? throw new ArgumentException("Seleccione un DIRECTORY activo.");
        if (!ArchivosDirectorios.EsDirectorioGestionado(directorio.Nombre))
            throw new ArgumentException("Este DIRECTORY se puede inspeccionar, pero no es uno de los destinos dedicados administrables de OraAdmin XE.");
        var tabla = await Bd.ConsultarAsync(SqlDirectorios.RespaldoParaOperacion, Parametro.De("id_respaldo", asociacion.IdRespaldo), Parametro.De("id_directorio", directorio.Id));
        if (tabla.Rows.Count != 1) throw new ArgumentException("El respaldo ya no pertenece a un DIRECTORY activo verificable.");
        var fila = tabla.Rows[0];
        var rutaCatalogo = (string)fila["RUTA_CATALOGO"];
        var rutaOracle = (string)fila["DIRECTORY_PATH"];
        if (!ArchivosDirectorios.RutasCoinciden(rutaCatalogo, rutaOracle)) throw new ArgumentException("La ruta Oracle y el catálogo ya no coinciden.");
        var ruta = ArchivosDirectorios.ValidarRutaLectura(rutaCatalogo);
        if (!Directory.Exists(ruta)) throw new DirectoryNotFoundException("La carpeta del DIRECTORY ya no existe para esta cuenta.");
        var archivoActual = (string)fila["ARCHIVO_DUMP"];
        if (!ArchivosDirectorios.CoincideNombre(archivoActual, archivo.Nombre)) throw new ArgumentException("La asociación del DMP cambió; refresque e intente de nuevo.");
        var dp = Convert.ToInt32(fila["JOB_DATAPUMP_ACTIVO"]) != 0;
        var scheduler = Convert.ToInt32(fila["JOB_SCHEDULER_ACTIVO"]) != 0;
        var restauracion = Convert.ToInt32(fila["RESTAURACION_ACTIVA"]) != 0;
        if (!ArchivosDirectorios.PuedeMutarDump((string)fila["ESTADO"], dp, scheduler, restauracion))
            throw new ArgumentException("No se puede modificar: el respaldo no está COMPLETADO o hay una operación activa relacionada.");
        await VerificarPrivilegiosAsync((string)fila["NOMBRE_ORACLE"]);
        var rutaArchivo = Path.Combine(ruta, archivo.Nombre);
        if (exigirArchivo)
        {
            if (!File.Exists(rutaArchivo)) throw new FileNotFoundException("El DMP ya no está en la carpeta.", archivo.Nombre);
            if ((File.GetAttributes(rutaArchivo) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("No se puede operar sobre un enlace/reparse point.");
            var actual = new FileInfo(rutaArchivo);
            if (actual.Length != archivo.Bytes || actual.LastWriteTime != archivo.Modificado)
                throw new ArgumentException("El DMP cambió desde la inspección. Refresque y confirme nuevamente.");
        }
        return new ContextoOperacion(Convert.ToInt32(fila["ID_RESPALDO"]), Convert.ToInt32(fila["ID_DIRECTORIO"]), (string)fila["NOMBRE_ORACLE"], ruta);
    }

    private async Task VerificarPrivilegiosAsync(string directorio)
    {
        var tabla = await Bd.ConsultarAsync(SqlDirectorios.PrivilegiosArchivosSesion, Parametro.De("directorio", directorio));
        var privilegios = tabla.Rows.Cast<DataRow>().Select(r => (string)r["PRIVILEGIO"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!privilegios.IsSupersetOf(["DIRECTORY_READ", "DIRECTORY_WRITE", "UTL_FILE"]))
            throw new ArgumentException("No se puede verificar READ, WRITE y EXECUTE sobre UTL_FILE para la sesión actual; se bloqueó la mutación.");
    }

    private async Task<string> RevalidarDirectorioAsync()
    {
        var directorio = DirectorioSeleccionado ?? throw new ArgumentException("Seleccione un DIRECTORY activo.");
        var tabla = await Bd.ConsultarAsync(SqlDirectorios.DirectorioPorNombre, Parametro.De("nombre", directorio.Nombre));
        if (tabla.Rows.Count != 1 || tabla.Rows[0]["ID_DIRECTORIO"] is DBNull || (string)tabla.Rows[0]["ACTIVO"] != "SI")
            throw new ArgumentException("El DIRECTORY ya no está activo y registrado.");
        var fila = tabla.Rows[0];
        if (Convert.ToInt32(fila["ID_DIRECTORIO"]) != directorio.Id || fila["RUTA_CATALOGO"] is not string rutaCatalogo || fila["DIRECTORY_PATH"] is not string rutaOracle ||
            !ArchivosDirectorios.RutasCoinciden(rutaCatalogo, rutaOracle))
            throw new ArgumentException("La ruta Oracle y el catálogo no coinciden.");
        var ruta = ArchivosDirectorios.ValidarRutaLectura(rutaCatalogo);
        if (!Directory.Exists(ruta)) throw new DirectoryNotFoundException("La carpeta ya no existe.");
        return ruta;
    }

    private async Task<string> RevalidarLogAsync(ArchivoListado archivo)
    {
        if (archivo.Asociacion is null || !ArchivosDirectorios.EsBasenameSeguro(archivo.Nombre, ".log"))
            throw new ArgumentException("Solo se pueden leer logs asociados exactamente al catálogo.");
        var directorio = DirectorioSeleccionado ?? throw new ArgumentException("Seleccione un DIRECTORY activo.");
        var catalogo = await Bd.ConsultarAsync(SqlDirectorios.CatalogoArchivosDeDirectorio, Parametro.De("id_directorio", directorio.Id));
        var vigente = catalogo.AsEnumerable().Any(f => Convert.ToInt32(f["ID_RESPALDO"]) == archivo.Asociacion.IdRespaldo &&
            ArchivosDirectorios.CoincideNombre((string)f["ARCHIVO"], archivo.Nombre) && ((string)f["TIPO"]).StartsWith("LOG_", StringComparison.Ordinal));
        if (!vigente) throw new ArgumentException("La asociación del log cambió; refresque e intente de nuevo.");
        var ruta = await RevalidarDirectorioAsync();
        var archivoRuta = Path.Combine(ruta, archivo.Nombre);
        if (!File.Exists(archivoRuta)) throw new FileNotFoundException("El archivo ya no está en la carpeta.", archivo.Nombre);
        if ((File.GetAttributes(archivoRuta) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("No se puede leer un enlace/reparse point.");
        return archivoRuta;
    }

    private sealed record DirectorioActivo(int Id, string Nombre, string RutaCatalogo, string RutaOracle)
    {
        public override string ToString() => $"{Nombre}  —  {RutaCatalogo}";
    }
    private sealed record AsociacionArchivo(int IdRespaldo, string Archivo, string Tipo, string Job, string Estado, bool RestauracionActiva);
    private sealed record ArchivoListado(string Nombre, long Bytes, DateTime Modificado, AsociacionArchivo? Asociacion);
    private sealed record RespaldoAusente(int IdRespaldo, int IdDirectorio, string Archivo, string Job, string Estado);
    private sealed record ContextoOperacion(int IdRespaldo, int IdDirectorio, string Directorio, string Ruta);
}

using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Tablespaces;

/// <summary>
/// Tablespaces: consulta de uso y archivos físicos en modo solo lectura (issue #15).
/// Las acciones CREATE, ALTER y DROP corresponden a la issue #16.
/// </summary>
public partial class UcTablespaces : ModuloBase
{
    private const long Mb = 1024L * 1024L;
    private const string AdvertenciaCapacidadXe = "-- Advertencia: Oracle XE valida finalmente la capacidad disponible y sus límites de almacenamiento.\r\n";
    private readonly SemaphoreSlim _cargaArchivos = new(1, 1);
    private readonly HashSet<string> _privilegios = new(StringComparer.OrdinalIgnoreCase);
    private bool _cargando;
    private bool _operacion;
    private bool _enXepdb1;
    private int _solicitudArchivos;
    private string? _tablespaceSeleccionado;

    public UcTablespaces()
    {
        InitializeComponent();
        ConfigurarGrilla(grdTablespaces, "Sin datos de tablespaces", "\uEDA2");
        ConfigurarGrilla(grdArchivos, "Seleccione un tablespace para ver sus archivos", "\uE8B7");

        Tema.ColumnaEstado(grdTablespaces, "STATUS");
        Tema.ColumnaEstado(grdTablespaces, "CONTENTS", _ => TipoEstado.Info);
        Tema.ColumnaEstado(grdTablespaces, "AUTOEXTEND", texto => texto == "MIXED" ? TipoEstado.Advertencia : Estados.Clasificar(texto));
        Tema.ColumnaEstado(grdArchivos, "TIPO_ARCHIVO", _ => TipoEstado.Info);
        Tema.ColumnaEstado(grdArchivos, "AUTOEXTENSIBLE");
        Tema.ColumnaEstado(grdArchivos, "ESTADO");
        grdTablespaces.CellPainting += GrdUso_CellPainting;
        grdArchivos.CellPainting += GrdUso_CellPainting;
        grdArchivos.CellToolTipTextNeeded += GrdArchivos_CellToolTipTextNeeded;
        grdArchivos.SelectionChanged += (_, _) => ActualizarAcciones();
        btnCrear.Click += async (_, _) => await CrearAsync();
        btnAgregarArchivo.Click += async (_, _) => await AgregarDatafileAsync();
        btnRedimensionar.Click += async (_, _) => await RedimensionarAsync();
        btnAutoextend.Click += async (_, _) => await AutoextendAsync();
        btnOffline.Click += async (_, _) => await CambiarEstadoAsync(EstadoTablespace.Offline);
        btnOnline.Click += async (_, _) => await CambiarEstadoAsync(EstadoTablespace.Online);
        btnSoloLectura.Click += async (_, _) => await CambiarEstadoAsync(EstadoTablespace.SoloLectura);
        btnLecturaEscritura.Click += async (_, _) => await CambiarEstadoAsync(EstadoTablespace.LecturaEscritura);
        btnEliminar.Click += async (_, _) => await EliminarAsync();
        ActualizarAcciones();
    }

    public override Modulo Modulo => Modulo.Tablespaces;

    public override Task AlActivarAsync() => CargarAsync();

    public override void AlDesactivar()
    {
        // Invalida una carga de detalle en espera antes de cambiar de módulo.
        _solicitudArchivos++;
    }

    private async void BtnRefrescar_Click(object? sender, EventArgs e) => await CargarAsync();

    private async Task CargarAsync()
    {
        if (_cargando || !PuedeActualizarUi || string.IsNullOrEmpty(Bd.Usuario)) return;

        var seleccionAnterior = _tablespaceSeleccionado;
        _cargando = true;
        btnRefrescar.Enabled = false;
        UseWaitCursor = true;
        Tema.Cargando(grdTablespaces, true);
        try
        {
            var uso = Bd.ConsultarAsync(SqlTablespaces.Uso);
            var privilegios = Bd.ConsultarAsync(SqlTablespaces.PrivilegiosSesion);
            await Task.WhenAll(uso, privilegios);
            if (!PuedeActualizarUi) return;
            CargarPrivilegios(privilegios.Result);
            var tabla = uso.Result;
            MostrarTablespaces(tabla);

            var seleccionado = RestaurarSeleccion(tabla, seleccionAnterior);
            _tablespaceSeleccionado = seleccionado;
            if (seleccionado is null)
            {
                MostrarArchivos(new DataTable());
                return;
            }

            await CargarArchivosAsync(seleccionado, ++_solicitudArchivos);
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi)
                Dialogos.Error(this, ex);
        }
        finally
        {
            if (PuedeActualizarUi)
            {
                Tema.Cargando(grdTablespaces, false);
                btnRefrescar.Enabled = true;
                UseWaitCursor = false;
                ActualizarAcciones();
            }
            _cargando = false;
        }
    }

    private void GrdTablespaces_SelectionChanged(object? sender, EventArgs e)
    {
        if (_cargando || !PuedeActualizarUi) return;
        ActualizarAcciones();

        var tablespace = TablespaceSeleccionado();
        if (tablespace is null || tablespace == _tablespaceSeleccionado) return;

        _tablespaceSeleccionado = tablespace;
        var solicitud = ++_solicitudArchivos;
        try
        {
            BeginInvoke(new Action(() => _ = CargarArchivosAsync(tablespace, solicitud)));
        }
        catch (InvalidOperationException) when (!PuedeActualizarUi)
        {
            // El control se estaba cerrando entre la comprobación anterior y BeginInvoke.
        }
    }

    private async Task CargarArchivosAsync(string tablespace, int solicitud)
    {
        await _cargaArchivos.WaitAsync();
        try
        {
            // Si hubo selecciones rápidas, solo la respuesta más reciente actualiza la UI.
            if (!PuedeActualizarUi || solicitud != _solicitudArchivos) return;

            Tema.Cargando(grdArchivos, true);
            var tabla = await Bd.ConsultarAsync(SqlTablespaces.Archivos, Parametro.De("tablespace", tablespace));
            if (!PuedeActualizarUi || solicitud != _solicitudArchivos) return;
            MostrarArchivos(tabla);
        }
        catch (Exception ex)
        {
            if (PuedeActualizarUi && solicitud == _solicitudArchivos)
                Dialogos.Error(this, ex);
        }
        finally
        {
            if (PuedeActualizarUi && solicitud == _solicitudArchivos)
            {
                Tema.Cargando(grdArchivos, false);
                ActualizarAcciones();
            }
            _cargaArchivos.Release();
        }
    }

    private async Task CrearAsync()
    {
        try
        {
            await AsegurarPrivilegiosAsync("CREATE TABLESPACE");
            var directorio = await DirectorioPdbAsync();
            var maximoMb = await MaximoMbBloquePredeterminadoAsync();
            using var dlg = new DlgTablespace(await SugerirRutaLibreAsync(directorio, "TS_VENTAS", 1), directorio, maximoMb);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var nombre = DdlTablespaces.ValidarNombrePrueba(dlg.Nombre);
            var ruta = await ValidarRutaNuevaAsync(dlg.Ruta, directorio);
            var existente = await Bd.ConsultarAsync(SqlTablespaces.MetadatosTablespace, Parametro.De("tablespace", nombre));
            if (existente.Rows.Count != 0) throw new ArgumentException($"El tablespace {nombre} ya existe.");
            var sql = AdvertenciaCapacidadXe + DdlTablespaces.Crear(nombre, ruta, dlg.InicialMb, dlg.Autoextend, dlg.NextMb, dlg.MaxMb, maximoMb);
            await EjecutarDdlAsync("Crear tablespace", "Crear tablespace", sql, false, $"Tablespace {nombre} creado.",
                () => DdlTablespaces.RegistrarCreadoEnSesion(nombre), async () =>
                {
                    await AsegurarPrivilegiosAsync("CREATE TABLESPACE");
                    if ((await Bd.ConsultarAsync(SqlTablespaces.MetadatosTablespace, Parametro.De("tablespace", nombre))).Rows.Count != 0)
                        throw new InvalidOperationException($"El tablespace {nombre} apareció durante la confirmación.");
                    await ValidarRutaNuevaAsync(ruta, directorio);
                });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task AgregarDatafileAsync()
    {
        try
        {
            var seleccionado = CapturarTablespace();
            await AsegurarPrivilegiosAsync("ALTER TABLESPACE");
            var meta = await RevalidarTablespaceAsync(seleccionado.Nombre);
            AsegurarElegible(meta, requiereOnline: true);
            if (meta.Bigfile) throw new InvalidOperationException("Los tablespaces BIGFILE no admiten ADD DATAFILE.");
            var directorio = await DirectorioPdbAsync();
            var maximoMb = MaximoMbSmallfile(meta.BlockSize);
            using var dlg = new DlgDatafile(ModoDatafile.Agregar, await SugerirRutaLibreAsync(directorio, meta.Nombre, 2), 20, maximoMb: maximoMb);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            meta = await RevalidarTablespaceAsync(meta.Nombre);
            AsegurarElegible(meta, requiereOnline: true);
            if (meta.Bigfile) throw new InvalidOperationException("El tablespace cambió a BIGFILE; no se agrega un datafile.");
            var ruta = await ValidarRutaNuevaAsync(dlg.Ruta, directorio);
            var sql = AdvertenciaCapacidadXe + DdlTablespaces.AgregarDatafile(meta.Nombre, ruta, dlg.TamanoMb, maximoMb);
            await EjecutarDdlAsync("Agregar datafile", "Agregar datafile", sql, false, "Datafile agregado.", null, async () =>
            {
                await AsegurarPrivilegiosAsync("ALTER TABLESPACE");
                var final = await RevalidarTablespaceAsync(meta.Nombre);
                AsegurarElegible(final, requiereOnline: true);
                if (final.Bigfile) throw new InvalidOperationException("El tablespace cambió a BIGFILE durante la confirmación.");
                await ValidarRutaNuevaAsync(ruta, directorio);
            });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task RedimensionarAsync()
    {
        try
        {
            var seleccionado = CapturarDatafile();
            await AsegurarPrivilegiosAsync("ALTER DATABASE");
            await AsegurarArchivosManualesAsync();
            var meta = await RevalidarDatafileAsync(seleccionado);
            AsegurarDatafileElegible(meta);
            var directorio = await DirectorioPdbAsync();
            ValidarDatafileExistente(meta.Ruta, directorio);
            var maximoMb = MaximoResizeMb(meta);
            using var dlg = new DlgDatafile(ModoDatafile.Redimensionar, meta.Ruta, ARedondearMb(meta.Bytes), maximoMb: maximoMb);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            meta = await RevalidarDatafileAsync(seleccionado);
            AsegurarDatafileElegible(meta);
            ValidarDatafileExistente(meta.Ruta, directorio);
            var aviso = dlg.TamanoMb < ARedondearMb(meta.Bytes)
                ? "-- Advertencia: Oracle puede rechazar la reducción si hay extensiones por encima del nuevo límite.\r\n"
                : "";
            var sql = AdvertenciaCapacidadXe + aviso + DdlTablespaces.RedimensionarDatafile(meta.Ruta, dlg.TamanoMb, MaximoResizeMb(meta));
            await EjecutarDdlAsync("Redimensionar datafile", "Redimensionar datafile", sql, false, "Datafile redimensionado.", null, async () =>
            {
                await AsegurarPrivilegiosAsync("ALTER DATABASE");
                await AsegurarArchivosManualesAsync();
                var final = await RevalidarDatafileAsync(seleccionado);
                AsegurarDatafileElegible(final);
                ValidarDatafileExistente(final.Ruta, directorio);
                DdlTablespaces.ValidarTamano(dlg.TamanoMb, "nuevo tamaño", MaximoResizeMb(final));
            });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task AutoextendAsync()
    {
        try
        {
            var seleccionado = CapturarDatafile();
            await AsegurarPrivilegiosAsync("ALTER DATABASE");
            await AsegurarArchivosManualesAsync();
            var meta = await RevalidarDatafileAsync(seleccionado);
            AsegurarDatafileElegible(meta);
            var directorio = await DirectorioPdbAsync();
            ValidarDatafileExistente(meta.Ruta, directorio);
            var actual = ARedondearMb(meta.Bytes);
            var maximoMb = MaximoAutoextendMb(meta);
            using var dlg = new DlgDatafile(ModoDatafile.Autoextend, meta.Ruta, actual,
                Math.Max(actual + 1, AMbMaximo(meta.MaxBytes)), Math.Max(1, ARedondearMb(meta.IncrementoBytes)), maximoMb);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            meta = await RevalidarDatafileAsync(seleccionado);
            AsegurarDatafileElegible(meta);
            ValidarDatafileExistente(meta.Ruta, directorio);
            var sql = AdvertenciaCapacidadXe + DdlTablespaces.CambiarAutoextend(meta.Ruta, dlg.Autoextend,
                ARedondearMb(meta.Bytes), dlg.NextMb, dlg.MaxMb, MaximoAutoextendMb(meta));
            await EjecutarDdlAsync("Configurar autoextend", "Configurar autoextend", sql, false, "Autoextend actualizado.", null, async () =>
            {
                await AsegurarPrivilegiosAsync("ALTER DATABASE");
                await AsegurarArchivosManualesAsync();
                var final = await RevalidarDatafileAsync(seleccionado);
                AsegurarDatafileElegible(final);
                ValidarDatafileExistente(final.Ruta, directorio);
                DdlTablespaces.ValidarAutoextend(ARedondearMb(final.Bytes), dlg.NextMb, dlg.MaxMb, MaximoAutoextendMb(final));
            });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task CambiarEstadoAsync(EstadoTablespace destino)
    {
        try
        {
            var seleccionado = CapturarTablespace();
            await AsegurarPrivilegiosAsync("ALTER TABLESPACE", "MANAGE TABLESPACE");
            var meta = await RevalidarTablespaceAsync(seleccionado.Nombre);
            AsegurarElegible(meta, requiereOnline: false);
            if (!DdlTablespaces.TransicionValida(meta.Estado, destino))
                throw new InvalidOperationException($"No se permite pasar de {meta.Estado} a {destino}.");
            await EjecutarDdlAsync("Cambiar estado de tablespace", "Cambiar estado de tablespace", DdlTablespaces.CambiarEstado(meta.Nombre, destino), false, "Estado actualizado.", null, async () =>
            {
                await AsegurarPrivilegiosAsync("ALTER TABLESPACE", "MANAGE TABLESPACE");
                var final = await RevalidarTablespaceAsync(meta.Nombre);
                AsegurarElegible(final, requiereOnline: false);
                if (!DdlTablespaces.TransicionValida(final.Estado, destino))
                    throw new InvalidOperationException("El estado del tablespace cambió durante la confirmación.");
            });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EliminarAsync()
    {
        try
        {
            var seleccionado = CapturarTablespace();
            await AsegurarPrivilegiosAsync("DROP TABLESPACE");
            var meta = await RevalidarTablespaceAsync(seleccionado.Nombre);
            var dependencias = await DependenciasEliminarAsync(meta.Nombre);
            AsegurarEliminable(meta, dependencias);
            using var dlg = new DlgEliminarTablespace(meta.Nombre);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            // Revalidación inmediatamente antes del preview/DDL: la selección no puede redirigir DROP.
            meta = await RevalidarTablespaceAsync(meta.Nombre);
            dependencias = await DependenciasEliminarAsync(meta.Nombre);
            AsegurarEliminable(meta, dependencias);
            await EjecutarDdlAsync("Eliminar tablespace", "Eliminar tablespace", DdlTablespaces.Eliminar(meta.Nombre), true,
                $"Tablespace {meta.Nombre} eliminado.", () => DdlTablespaces.OlvidarCreadoEnSesion(meta.Nombre), async () =>
                {
                    await AsegurarPrivilegiosAsync("DROP TABLESPACE");
                    var final = await RevalidarTablespaceAsync(meta.Nombre);
                    var depsFinales = await DependenciasEliminarAsync(final.Nombre);
                    AsegurarEliminable(final, depsFinales);
                });
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async Task EjecutarDdlAsync(string accion, string titulo, string sql, bool destructiva, string exito,
        Action? despuesDeExito = null, Func<Task>? revalidarAntesDeEjecutar = null)
    {
        if (!Dialogos.ConfirmarSql(this, titulo, sql, destructiva)) return;
        _operacion = true;
        ActualizarAcciones();
        UseWaitCursor = true;
        try
        {
            await DdlTablespaces.EjecutarTrasRevalidarAsync(
                revalidarAntesDeEjecutar ?? (() => Task.CompletedTask),
                async () => await Bd.EjecutarAsync(Modulo, accion, sql));
            despuesDeExito?.Invoke();
            Dialogos.Exito(this, exito);
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        finally
        {
            UseWaitCursor = false;
            _operacion = false;
            // DDL es commit implícito; incluso ante error se releen metadatos, sin DDL compensatorio.
            await CargarAsync();
            ActualizarAcciones();
        }
    }

    private async Task AsegurarPrivilegiosAsync(params string[] requeridos)
    {
        CargarPrivilegios(await Bd.ConsultarAsync(SqlTablespaces.PrivilegiosSesion));
        if (!_enXepdb1) throw new InvalidOperationException("Las operaciones manuales solo están habilitadas en XEPDB1.");
        if (!requeridos.Any(TienePrivilegio))
            throw new InvalidOperationException($"Falta el privilegio requerido: {string.Join(" o ", requeridos)}.");
    }

    private async Task<string> DirectorioPdbAsync()
    {
        await AsegurarArchivosManualesAsync();
        var ruta = await Bd.EscalarAsync(SqlTablespaces.DatafileUsers) as string;
        if (string.IsNullOrWhiteSpace(ruta)) throw new InvalidOperationException("No se encontró el datafile de USERS para verificar el directorio de XEPDB1.");
        return DdlTablespaces.DirectorioDeDatafile(ruta);
    }

    private async Task<long> MaximoMbBloquePredeterminadoAsync()
    {
        var bloque = Convert.ToInt64(await Bd.EscalarAsync(SqlTablespaces.BloquePredeterminado)
            ?? throw new InvalidOperationException("No se pudo obtener DB_BLOCK_SIZE para validar el tamaño."));
        return MaximoMbSmallfile(bloque);
    }

    private async Task AsegurarArchivosManualesAsync()
    {
        var destinoOmg = await Bd.EscalarAsync(SqlTablespaces.DestinoOracleManagedFiles) as string;
        if (!string.IsNullOrWhiteSpace(destinoOmg))
            throw new InvalidOperationException("Oracle Managed Files está configurado; la administración manual de .DBF está deshabilitada.");
    }

    private async Task<string> SugerirRutaLibreAsync(string directorio, string tablespace, int desde)
    {
        for (var numero = desde; numero <= 99; numero++)
        {
            var candidata = DdlTablespaces.SugerirRutaDatafile(directorio, tablespace, numero);
            var registrada = Convert.ToInt64(await Bd.EscalarAsync(SqlTablespaces.DatafileRegistrado, Parametro.De("ruta", candidata)) ?? 0) > 0;
            if (!registrada && !File.Exists(candidata)) return candidata;
        }
        throw new InvalidOperationException("No hay un nombre .DBF libre entre 01 y 99 en el directorio verificado.");
    }

    private async Task<string> ValidarRutaNuevaAsync(string ruta, string directorio)
    {
        var validada = DdlTablespaces.ValidarRutaDatafile(ruta, directorio, verificarSistemaArchivos: true);
        var registrada = Convert.ToInt64(await Bd.EscalarAsync(SqlTablespaces.DatafileRegistrado, Parametro.De("ruta", validada)) ?? 0);
        if (registrada > 0) throw new ArgumentException("La ruta ya está registrada en DBA_DATA_FILES o DBA_TEMP_FILES.");
        return validada;
    }

    private static string ValidarDatafileExistente(string ruta, string directorio) =>
        DdlTablespaces.ValidarRutaDatafileExistente(ruta, directorio, verificarSistemaArchivos: true);

    private async Task<TablespaceMeta> RevalidarTablespaceAsync(string nombre)
    {
        var tabla = await Bd.ConsultarAsync(SqlTablespaces.MetadatosTablespace, Parametro.De("tablespace", nombre));
        if (tabla.Rows.Count != 1) throw new InvalidOperationException("El tablespace ya no existe o no se pudo identificar de forma única.");
        var fila = tabla.Rows[0];
        return new TablespaceMeta((string)fila["TABLESPACE_NAME"], (string)fila["CONTENTS"], (string)fila["STATUS"],
            string.Equals(fila["BIGFILE"] as string, "YES", StringComparison.OrdinalIgnoreCase), Convert.ToInt64(fila["BLOCK_SIZE"]));
    }

    private async Task<DatafileMeta> RevalidarDatafileAsync(SeleccionDatafile seleccionado)
    {
        var tabla = await Bd.ConsultarAsync(SqlTablespaces.MetadatosDatafile,
            Parametro.De("tablespace", seleccionado.Tablespace), Parametro.De("file_id", seleccionado.FileId), Parametro.De("file_name", seleccionado.Ruta));
        if (tabla.Rows.Count != 1) throw new InvalidOperationException("El datafile seleccionado cambió, desapareció o no coincide con la selección original.");
        var fila = tabla.Rows[0];
        var incremento = Convert.ToInt64(fila["INCREMENT_BY"]) * Convert.ToInt64(fila["BLOCK_SIZE"]);
        return new DatafileMeta((string)fila["TABLESPACE_NAME"], (string)fila["FILE_NAME"], Convert.ToInt64(fila["BYTES"]),
            Convert.ToInt64(fila["MAXBYTES"]), incremento, (string)fila["CONTENTS"], (string)fila["TABLESPACE_STATUS"],
            string.Equals(fila["BIGFILE"] as string, "YES", StringComparison.OrdinalIgnoreCase), Convert.ToInt64(fila["BLOCK_SIZE"]),
            string.Equals(fila["AUTOEXTENSIBLE"] as string, "YES", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<DependenciasEliminar> DependenciasEliminarAsync(string nombre)
    {
        var tabla = await Bd.ConsultarAsync(SqlTablespaces.DependenciasEliminar, Parametro.De("tablespace", nombre));
        if (tabla.Rows.Count != 1) throw new InvalidOperationException("No se pudieron verificar las dependencias para DROP.");
        var fila = tabla.Rows[0];
        if (fila["SEGMENTOS"] is DBNull || fila["USUARIOS_ASIGNADOS"] is DBNull || fila["OBJETOS_ASIGNADOS"] is DBNull ||
            fila["CUOTAS"] is DBNull || fila["PREDETERMINADOS_PDB"] is DBNull)
            throw new InvalidOperationException("Las dependencias para DROP no se pudieron verificar de forma confiable.");
        return new DependenciasEliminar(Convert.ToInt64(fila["SEGMENTOS"]), Convert.ToInt64(fila["USUARIOS_ASIGNADOS"]),
            Convert.ToInt64(fila["OBJETOS_ASIGNADOS"]), Convert.ToInt64(fila["CUOTAS"]), Convert.ToInt64(fila["PREDETERMINADOS_PDB"]));
    }

    private static void AsegurarElegible(TablespaceMeta meta, bool requiereOnline)
    {
        if (!DdlTablespaces.PuedeAdministrar(meta.Nombre, meta.Contenido))
            throw new InvalidOperationException("Solo se administran tablespaces permanentes de prueba creados en esta instancia; SYSTEM, SYSAUX, USERS, UNDO, TEMP y tablespaces preexistentes están protegidos.");
        if (requiereOnline && !string.Equals(meta.Estado, "ONLINE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La operación requiere un tablespace ONLINE y de lectura/escritura.");
    }

    private static void AsegurarDatafileElegible(DatafileMeta meta)
    {
        AsegurarElegible(new TablespaceMeta(meta.Tablespace, meta.Contenido, meta.EstadoTablespace, meta.Bigfile, meta.BlockSize), true);
        if (meta.Bigfile) throw new InvalidOperationException("BIGFILE se trata de forma conservadora: no se administra manualmente por datafile.");
    }

    private static void AsegurarEliminable(TablespaceMeta meta, DependenciasEliminar dependencias)
    {
        if (!DdlTablespaces.PuedeEliminar(meta.Nombre, meta.Contenido, dependencias.Segmentos, dependencias.Usuarios, dependencias.Objetos,
                dependencias.Cuotas, dependencias.PredeterminadosPdb))
            throw new InvalidOperationException("DROP bloqueado: se exige procedencia de esta sesión y cero segmentos, objetos, usuarios, cuotas y valores predeterminados. Tras reiniciar no se inventa procedencia.");
    }

    private void CargarPrivilegios(DataTable tabla)
    {
        _privilegios.Clear();
        _enXepdb1 = false;
        foreach (DataRow fila in tabla.Rows)
        {
            if (string.Equals(fila["CON_NAME"] as string, "XEPDB1", StringComparison.OrdinalIgnoreCase)) _enXepdb1 = true;
            if (fila["PRIVILEGE"] is string privilegio && privilegio.Length > 0) _privilegios.Add(privilegio);
        }
    }

    private bool TienePrivilegio(string privilegio) => _privilegios.Contains(privilegio);
    private static long ARedondearMb(long bytes) => bytes <= 0 ? 1 : bytes / Mb + (bytes % Mb == 0 ? 0 : 1);
    private static long AMbMaximo(long bytes) => bytes < Mb ? 0 : bytes / Mb;
    private static long MaximoMbSmallfile(long blockSize) => DdlTablespaces.MaximoMbSmallfile(blockSize);
    private static long MaximoResizeMb(DatafileMeta meta)
    {
        var porBloque = MaximoMbSmallfile(meta.BlockSize);
        return meta.Autoextensible && meta.MaxBytes > 0 ? Math.Min(porBloque, AMbMaximo(meta.MaxBytes)) : porBloque;
    }
    private static long MaximoAutoextendMb(DatafileMeta meta) => MaximoMbSmallfile(meta.BlockSize);

    private TablespaceSeleccion? TablespaceActual() => grdTablespaces.SelectedRows.Count == 1 && grdTablespaces.CurrentRow?.DataBoundItem is DataRowView fila
        ? new TablespaceSeleccion(fila.Row.Field<string>("TABLESPACE_NAME") ?? "", fila.Row.Field<string>("CONTENTS") ?? "", fila.Row.Field<string>("STATUS") ?? "") : null;

    private TablespaceSeleccion CapturarTablespace() => TablespaceActual() ?? throw new InvalidOperationException("Seleccione un tablespace.");

    private SeleccionDatafile? DatafileActual() => grdArchivos.SelectedRows.Count == 1 && grdArchivos.CurrentRow?.DataBoundItem is DataRowView fila
        ? new SeleccionDatafile(_tablespaceSeleccionado ?? "", Convert.ToInt64(fila.Row["FILE_ID"]), fila.Row.Field<string>("FILE_NAME") ?? "",
            string.Equals(fila.Row.Field<string>("TIPO_ARCHIVO"), "DATAFILE", StringComparison.Ordinal)) : null;

    private SeleccionDatafile CapturarDatafile()
    {
        var archivo = DatafileActual() ?? throw new InvalidOperationException("Seleccione un datafile.");
        if (!archivo.EsDatafile) throw new InvalidOperationException("Las operaciones sobre TEMPFILE están deshabilitadas por seguridad.");
        if (string.IsNullOrWhiteSpace(archivo.Tablespace) || string.IsNullOrWhiteSpace(archivo.Ruta)) throw new InvalidOperationException("La selección de datafile no es válida.");
        return archivo;
    }

    private void ActualizarAcciones()
    {
        var ts = TablespaceActual();
        var archivo = DatafileActual();
        var elegible = ts is not null && _enXepdb1 && !_operacion && DdlTablespaces.PuedeAdministrar(ts.Nombre, ts.Contenido);
        var archivoElegible = elegible && archivo is { EsDatafile: true };
        btnCrear.Enabled = _enXepdb1 && !_operacion && TienePrivilegio("CREATE TABLESPACE");
        btnAgregarArchivo.Enabled = elegible && TienePrivilegio("ALTER TABLESPACE") && ts!.Estado == "ONLINE";
        btnRedimensionar.Enabled = archivoElegible && TienePrivilegio("ALTER DATABASE");
        btnAutoextend.Enabled = archivoElegible && TienePrivilegio("ALTER DATABASE");
        var puedeEstado = elegible && (TienePrivilegio("ALTER TABLESPACE") || TienePrivilegio("MANAGE TABLESPACE"));
        btnOffline.Enabled = puedeEstado && DdlTablespaces.TransicionValida(ts!.Estado, EstadoTablespace.Offline);
        btnOnline.Enabled = puedeEstado && DdlTablespaces.TransicionValida(ts!.Estado, EstadoTablespace.Online);
        btnSoloLectura.Enabled = puedeEstado && DdlTablespaces.TransicionValida(ts!.Estado, EstadoTablespace.SoloLectura);
        btnLecturaEscritura.Enabled = puedeEstado && DdlTablespaces.TransicionValida(ts!.Estado, EstadoTablespace.LecturaEscritura);
        btnEliminar.Enabled = elegible && TienePrivilegio("DROP TABLESPACE") && DdlTablespaces.FueCreadoEnSesion(ts!.Nombre);
    }

    private sealed record TablespaceSeleccion(string Nombre, string Contenido, string Estado);
    private sealed record SeleccionDatafile(string Tablespace, long FileId, string Ruta, bool EsDatafile);
    private sealed record TablespaceMeta(string Nombre, string Contenido, string Estado, bool Bigfile, long BlockSize);
    private sealed record DatafileMeta(string Tablespace, string Ruta, long Bytes, long MaxBytes, long IncrementoBytes, string Contenido,
        string EstadoTablespace, bool Bigfile, long BlockSize, bool Autoextensible);
    private sealed record DependenciasEliminar(long Segmentos, long Usuarios, long Objetos, long Cuotas, long PredeterminadosPdb);

    private void ConfigurarGrilla(DataGridView grilla, string mensajeVacio, string icono)
    {
        Tema.AplicarGrilla(grilla);
        grilla.AutoSizeColumnsMode = ReferenceEquals(grilla, grdArchivos)
            ? DataGridViewAutoSizeColumnsMode.None
            : DataGridViewAutoSizeColumnsMode.Fill;
        grilla.ScrollBars = ReferenceEquals(grilla, grdArchivos) ? ScrollBars.Both : ScrollBars.Vertical;
        grilla.RowTemplate.Height = 36;
        Tema.MensajeVacio(grilla, mensajeVacio, icono);
    }

    private void MostrarTablespaces(DataTable tabla)
    {
        grdTablespaces.DataSource = tabla;
        ConfigurarColumnas(grdTablespaces,
            ("TABLESPACE_NAME", "Tablespace", 16), ("CONTENTS", "Tipo", 13), ("STATUS", "Estado", 11),
            ("TOTAL_MB", "Total (MB)", 10), ("USADO_MB", "Usado (MB)", 10), ("LIBRE_MB", "Libre (MB)", 10),
            ("PCT_USADO", "% usado", 24), ("AUTOEXTEND", "Autoextend", 10));
    }

    private void MostrarArchivos(DataTable tabla)
    {
        grdArchivos.DataSource = tabla;
        ConfigurarColumnas(grdArchivos,
            ("TIPO_ARCHIVO", "Archivo", 10), ("FILE_ID", "ID", 6), ("FILE_NAME", "Ruta", 31),
            ("TAMANO_MB", "Tamaño (MB)", 10), ("USADO_MB", "Usado (MB)", 10), ("LIBRE_MB", "Libre (MB)", 10),
            ("PCT_USADO", "% usado", 20), ("AUTOEXTENSIBLE", "Autoextend", 10), ("MAX_MB", "Máximo (MB)", 11),
            ("ESTADO", "Estado", 12));
        ConfigurarAnchosArchivos();
    }

    private static void ConfigurarColumnas(DataGridView grilla, params (string Nombre, string Encabezado, float Peso)[] columnas)
    {
        foreach (var (nombre, encabezado, peso) in columnas)
        {
            var columna = grilla.Columns[nombre];
            if (columna is null) continue;
            columna.HeaderText = encabezado;
            columna.FillWeight = peso;
            if (nombre is "FILE_ID" or "TOTAL_MB" or "USADO_MB" or "LIBRE_MB" or "TAMANO_MB" or "MAX_MB")
                columna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void ConfigurarAnchosArchivos()
    {
        var anchos = new (string Nombre, int Ancho, int Minimo)[]
        {
            ("TIPO_ARCHIVO", 95, 85), ("FILE_ID", 65, 60), ("FILE_NAME", 520, 360),
            ("TAMANO_MB", 115, 105), ("USADO_MB", 110, 100), ("LIBRE_MB", 110, 100),
            ("PCT_USADO", 150, 130), ("AUTOEXTENSIBLE", 120, 110), ("MAX_MB", 125, 110), ("ESTADO", 120, 100)
        };
        foreach (var (nombre, ancho, minimo) in anchos)
        {
            var columna = grdArchivos.Columns[nombre];
            if (columna is null) continue;
            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columna.MinimumWidth = minimo;
            columna.Width = ancho;
        }
    }

    private void GrdArchivos_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || grdArchivos.Columns[e.ColumnIndex].Name != "FILE_NAME") return;
        if (grdArchivos.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string ruta && ruta.Length > 0)
            e.ToolTipText = ruta;
    }

    private string? RestaurarSeleccion(DataTable tabla, string? preferido)
    {
        if (tabla.Rows.Count == 0) return null;
        var nombre = tabla.AsEnumerable().Select(f => f.Field<string>("TABLESPACE_NAME"))
            .FirstOrDefault(n => n == preferido) ?? tabla.Rows[0].Field<string>("TABLESPACE_NAME");

        foreach (DataGridViewRow fila in grdTablespaces.Rows)
        {
            if (!string.Equals(fila.Cells["TABLESPACE_NAME"].Value as string, nombre, StringComparison.Ordinal)) continue;
            fila.Selected = true;
            grdTablespaces.CurrentCell = fila.Cells["TABLESPACE_NAME"];
            break;
        }
        return nombre;
    }

    private string? TablespaceSeleccionado() => grdTablespaces.CurrentRow?.DataBoundItem is DataRowView fila
        ? fila.Row.Field<string>("TABLESPACE_NAME")
        : null;

    private bool PuedeActualizarUi => !IsDisposed && !Disposing;

    /// <summary>Dibuja porcentaje real con una barra verde, amarilla o roja.</summary>
    private static void GrdUso_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grilla || e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null ||
            grilla.Columns[e.ColumnIndex].Name != "PCT_USADO") return;

        e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
        var porcentaje = ConvertirPorcentaje(e.Value);
        if (porcentaje is null)
        {
            TextRenderer.DrawText(e.Graphics, "N/D", Tema.Fuente, e.CellBounds, Tema.TextoSecundario,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            e.Handled = true;
            return;
        }

        var normalizado = IndicadorUsoTablespace.Normalizar(porcentaje)!.Value;
        var ancho = Math.Max(10, e.CellBounds.Width - 70);
        var barra = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + e.CellBounds.Height / 2 - 4, ancho, 8);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var fondo = Tema.Redondeado(barra, 4))
        using (var pincel = new SolidBrush(Tema.SuperficieAlta))
            e.Graphics.FillPath(pincel, fondo);
        var lleno = (int)(barra.Width * normalizado / 100);
        if (lleno > 0)
        {
            using var relleno = Tema.Redondeado(new Rectangle(barra.X, barra.Y, Math.Max(2, lleno), barra.Height), 4);
            using var color = new SolidBrush(Tema.ColoresDe(IndicadorUsoTablespace.Clasificar(porcentaje)).Texto);
            e.Graphics.FillPath(color, relleno);
        }

        var texto = new Rectangle(barra.Right + 6, e.CellBounds.Y, 56, e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics, $"{normalizado:0.0} %", Tema.Fuente, texto, Tema.Texto,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        e.Handled = true;
    }

    private static double? ConvertirPorcentaje(object? valor)
    {
        if (valor is null or DBNull) return null;
        try { return Convert.ToDouble(valor); }
        catch (Exception) when (valor is IConvertible) { return null; }
    }
}

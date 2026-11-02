using System.Data;
using OraAdminXE.Datos;
using OraAdminXE.UI;

namespace OraAdminXE.Modulos.Directorios;

/// <summary>
/// Pestaña de administración de objetos DIRECTORY. La pestaña de archivos se agregará en la issue #18
/// sin mezclar operaciones de sistema de archivos con este control.
/// </summary>
public sealed class UcObjetosDirectorios : UserControl
{
    private readonly DataGridView _grilla = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _privilegios = new() { Dock = DockStyle.Fill };
    private readonly Label _lblPie = new()
    {
        Dock = DockStyle.Fill, AutoSize = false, ForeColor = Tema.TextoSecundario,
        Padding = new Padding(0, 4, 0, 4), TextAlign = ContentAlignment.TopLeft
    };
    private readonly BotonPlano _btnCrear = Tema.Boton("Crear DIRECTORY…", primario: true, icono: "");
    private readonly BotonPlano _btnRegistrar = Tema.Boton("Registrar existente", icono: "");
    private readonly BotonPlano _btnOtorgar = Tema.Boton("Otorgar READ/WRITE…", icono: "");
    private readonly BotonPlano _btnEliminar = Tema.Boton("Eliminar", icono: "");
    private readonly BotonPlano _btnRefrescar = Tema.Boton("Refrescar", icono: "");
    private bool _ocupado;
    private bool _cargando;
    private bool _cargandoPrivilegios;
    private bool _recargarPrivilegios;
    // ADMIN_BD.DIRECTORIO no guarda proveniencia. Esta evidencia efímera solo permite administrar
    // el objeto aislado creado por esta instancia de la aplicación, nunca uno preexistente.
    private readonly Dictionary<string, string> _creadosEnEstaSesion = new(StringComparer.OrdinalIgnoreCase);

    private static IBaseDatos Bd => Sesion.Bd;

    public UcObjetosDirectorios()
    {
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        Padding = new Padding(16, 12, 16, 8);
        Font = Tema.Fuente;
        _btnEliminar.Estilo = EstiloBoton.Peligro;

        var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        izquierda.Controls.AddRange([_btnCrear, _btnRegistrar, _btnOtorgar]);
        var derecha = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        derecha.Controls.AddRange([_btnEliminar, _btnRefrescar]);
        var barra = new Panel { Dock = DockStyle.Top, Height = 48 };
        barra.Controls.Add(izquierda);
        barra.Controls.Add(derecha);

        ConfigurarGrillaDirectorios();
        ConfigurarGrillaPrivilegios();
        var etiquetaPrivilegios = new Label
        {
            Text = "Privilegios del DIRECTORY seleccionado", Dock = DockStyle.Top, Height = 30,
            Font = Tema.FuenteNegrita, ForeColor = Tema.TextoSecundario, TextAlign = ContentAlignment.MiddleLeft
        };
        var panelPrivilegios = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        panelPrivilegios.Controls.Add(_privilegios);
        panelPrivilegios.Controls.Add(etiquetaPrivilegios);
        var separador = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320,
            Panel1MinSize = 180, Panel2MinSize = 110, IsSplitterFixed = false, BackColor = Tema.Borde
        };
        separador.Panel1.Padding = new Padding(0, 0, 0, 1);
        separador.Panel1.Controls.Add(_grilla);
        separador.Panel2.Controls.Add(panelPrivilegios);

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 48 };
        pie.Controls.Add(_lblPie);
        Controls.Add(separador);
        Controls.Add(pie);
        Controls.Add(barra);

        _btnRefrescar.Click += async (_, _) => await CargarAsync();
        _btnCrear.Click += async (_, _) => await CrearAsync();
        _btnRegistrar.Click += async (_, _) => await RegistrarAsync();
        _btnOtorgar.Click += async (_, _) => await OtorgarAsync();
        _btnEliminar.Click += async (_, _) => await EliminarAsync();
        _grilla.SelectionChanged += async (_, _) => await AlCambiarSeleccionAsync();
        ActualizarBotones();
    }

    private void ConfigurarGrillaDirectorios()
    {
        Tema.AplicarGrilla(_grilla);
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grilla.MultiSelect = false;
        _grilla.RowTemplate.Height = 36;
        _grilla.ShowCellToolTips = true;
        Tema.ColumnaEstado(_grilla, "REGISTRADO", texto => texto == "SI" ? TipoEstado.Ok : TipoEstado.Neutro);
        Tema.ColumnaEstado(_grilla, "ACTIVO", texto => texto == "SI" ? TipoEstado.Ok : TipoEstado.Neutro);
        Tema.ColumnaEstado(_grilla, "CARPETA_LOCAL", texto => texto == "SI" ? TipoEstado.Ok : TipoEstado.Peligro);
        Tema.MensajeVacio(_grilla, "No hay objetos DIRECTORY visibles en la PDB.", "");
        _grilla.CellFormatting += (sender, e) =>
        {
            if (e.RowIndex < 0 || e.CellStyle is null || _grilla.Rows[e.RowIndex].DataBoundItem is not DataRowView fila) return;
            if (e.ColumnIndex >= 0 && _grilla.Columns[e.ColumnIndex].Name == "DIRECTORY_PATH" && fila["DIRECTORY_PATH"] is string ruta)
                _grilla.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = ruta;
            if (fila["REGISTRADO"] as string == "NO") e.CellStyle.ForeColor = Tema.TextoSecundario;
        };
    }

    private void ConfigurarGrillaPrivilegios()
    {
        Tema.AplicarGrilla(_privilegios);
        _privilegios.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _privilegios.MultiSelect = false;
        _privilegios.RowTemplate.Height = 32;
        Tema.ColumnaEstado(_privilegios, "PRIVILEGE", _ => TipoEstado.Acento);
        Tema.MensajeVacio(_privilegios, "Seleccione un DIRECTORY para ver sus privilegios.", "");
    }

    private DataRow? Seleccionado =>
        _grilla.SelectedRows.Count > 0 && _grilla.SelectedRows[0].DataBoundItem is DataRowView vista ? vista.Row : null;

    private static string Nombre(DataRow fila) => (string)fila["DIRECTORY_NAME"];
    private static string Ruta(DataRow fila) => (string)fila["DIRECTORY_PATH"];
    private static bool Registrado(DataRow fila) => fila["ID_DIRECTORIO"] is not DBNull;
    private static bool Activo(DataRow fila) => string.Equals(fila["ACTIVO"] as string, "SI", StringComparison.Ordinal);

    public async Task CargarAsync(string? seleccionar = null)
    {
        if (string.IsNullOrEmpty(Bd.Usuario)) return;
        _cargando = true;
        ActualizarEstadoInteraccion();
        Tema.Cargando(_grilla, true);
        try
        {
            var objetivo = seleccionar;
            if (objetivo is null && Seleccionado is { } anterior) objetivo = Nombre(anterior);
            var tabla = await Bd.ConsultarAsync(SqlDirectorios.Directorios);
            tabla.Columns.Add("CARPETA_LOCAL", typeof(string));
            foreach (DataRow fila in tabla.Rows)
                fila["CARPETA_LOCAL"] = ExisteCarpetaLocal(Ruta(fila)) ? "SI" : "NO";
            _grilla.DataSource = tabla;
            EncabezadosDirectorios();
            if (objetivo is not null)
                foreach (DataGridViewRow fila in _grilla.Rows)
                    if (string.Equals(fila.Cells["DIRECTORY_NAME"].Value as string, objetivo, StringComparison.Ordinal))
                    {
                        fila.Selected = true;
                        _grilla.CurrentCell = fila.Cells["DIRECTORY_NAME"];
                        break;
                    }
            var registrados = tabla.AsEnumerable().Count(Registrado);
            var activos = tabla.AsEnumerable().Count(f => Registrado(f) && Activo(f));
            _lblPie.Text = $"{tabla.Rows.Count} DIRECTORY · {registrados} registrados · {activos} activos · «Carpeta local» no comprueba permisos de OracleServiceXE. GRANT y DROP solo se habilitan para el DIRECTORY de demostración creado en esta sesión.";
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            _cargando = false;
            Tema.Cargando(_grilla, false);
            await CargarPrivilegiosAsync();
            ActualizarEstadoInteraccion();
        }
    }

    private static bool ExisteCarpetaLocal(string ruta)
    {
        try { return Directory.Exists(ruta); }
        catch (Exception) { return false; }
    }

    private void EncabezadosDirectorios()
    {
        var columnas = new Dictionary<string, (string Texto, float Peso)>
        {
            ["DIRECTORY_NAME"] = ("DIRECTORY", 18), ["DIRECTORY_PATH"] = ("Ruta del servidor", 39),
            ["REGISTRADO"] = ("Registrado", 11), ["ACTIVO"] = ("Activo", 9), ["CARPETA_LOCAL"] = ("Carpeta local", 12),
            ["FECHA_REGISTRO"] = ("Registrado el", 14)
        };
        foreach (DataGridViewColumn columna in _grilla.Columns)
        {
            if (columnas.TryGetValue(columna.Name, out var datos))
            {
                columna.HeaderText = datos.Texto;
                columna.FillWeight = datos.Peso;
            }
            else columna.Visible = false;
            if (columna.ValueType == typeof(DateTime)) columna.DefaultCellStyle.Format = "dd/MM/yy HH:mm";
        }
    }

    private async Task AlCambiarSeleccionAsync()
    {
        if (_cargando) return;
        ActualizarBotones();
        await CargarPrivilegiosAsync();
    }

    private async Task CargarPrivilegiosAsync()
    {
        if (_cargandoPrivilegios)
        {
            _recargarPrivilegios = true;
            return;
        }
        _cargandoPrivilegios = true;
        ActualizarEstadoInteraccion();
        try
        {
            do
            {
                _recargarPrivilegios = false;
                if (Seleccionado is not { } fila)
                {
                    _privilegios.DataSource = null;
                    continue;
                }
                var solicitado = Nombre(fila);
                Tema.Cargando(_privilegios, true);
                var tabla = await Bd.ConsultarAsync(SqlDirectorios.PrivilegiosDe, Parametro.De("directorio", solicitado));
                if (Seleccionado is not { } actual || !string.Equals(Nombre(actual), solicitado, StringComparison.Ordinal))
                {
                    _recargarPrivilegios = true;
                    continue;
                }
                _privilegios.DataSource = tabla;
                foreach (DataGridViewColumn columna in _privilegios.Columns)
                    columna.HeaderText = columna.Name switch { "GRANTEE" => "Beneficiario", "PRIVILEGE" => "Privilegio", "GRANTABLE" => "Con opción", _ => columna.Name };
            } while (_recargarPrivilegios);
        }
        catch (Exception ex)
        {
            Dialogos.Error(this, ex);
        }
        finally
        {
            Tema.Cargando(_privilegios, false);
            _cargandoPrivilegios = false;
            ActualizarEstadoInteraccion();
        }
    }

    private void ActualizarBotones()
    {
        var fila = Seleccionado;
        var modificable = fila is not null && Registrado(fila) && Activo(fila) && EsPropiedadVerificada(fila);
        var ocupado = _ocupado || _cargando || _cargandoPrivilegios;
        _btnCrear.Enabled = !ocupado && !string.IsNullOrEmpty(Bd.Usuario);
        _btnRefrescar.Enabled = !ocupado && !string.IsNullOrEmpty(Bd.Usuario);
        _btnRegistrar.Enabled = !ocupado && fila is not null && (!Registrado(fila) || !Activo(fila));
        _btnOtorgar.Enabled = !ocupado && modificable;
        _btnEliminar.Enabled = !ocupado && modificable;
    }

    private async Task CrearAsync()
    {
        using var dialogo = new DlgDirectorio();
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        await EjecutarOperacionAsync(async () =>
        {
            if (DdlDirectorios.EsNombreReservado(dialogo.Nombre))
                throw new ArgumentException($"{dialogo.Nombre} es un DIRECTORY reservado del proyecto u Oracle y no se puede crear ni redirigir desde la app.");
            if (!DdlDirectorios.EsNombreDemostracionPermitido(dialogo.Nombre))
                throw new ArgumentException($"Sin una columna de proveniencia persistente, la app solo permite crear el DIRECTORY aislado {DdlDirectorios.DirectorioDemostracion}.");
            if (Convert.ToInt32(await Bd.EscalarAsync(SqlDirectorios.ExisteDirectorio, Parametro.De("nombre", dialogo.Nombre))) > 0)
                throw new ArgumentException($"Ya existe un objeto DIRECTORY llamado {dialogo.Nombre}. Use «Registrar existente»; la app nunca lo reemplaza.");

            var grantAdminBd = DdlDirectorios.OtorgarLecturaEscritura(dialogo.Nombre, "ADMIN_BD");
            var sentencias = new List<string>
            {
                DdlDirectorios.Crear(dialogo.Nombre, dialogo.Ruta),
                "-- Se comprobarán grants directos/PUBLIC READ/WRITE de ADMIN_BD; el GRANT siguiente solo se ejecutará si faltan.",
                grantAdminBd,
                DdlDirectorios.RegistrarVista(dialogo.Nombre, dialogo.Ruta)
            };
            var avisoCarpeta = dialogo.CrearCarpeta ? $"-- Antes del SQL se creará la carpeta local confirmada: {dialogo.Ruta}\r\n" : "";
            if (!Dialogos.ConfirmarSql(this, "Crear DIRECTORY", avisoCarpeta + string.Join(";\r\n", sentencias) + ";")) return;

            // Tras la confirmación la ruta puede haber cambiado o haberse convertido en un reparse point.
            // Se valida de nuevo antes y después de cualquier creación física explícitamente autorizada.
            var rutaConfirmada = DdlDirectorios.ValidarRutaCreacion(dialogo.Ruta);
            if (!Directory.Exists(rutaConfirmada))
            {
                if (!dialogo.CrearCarpeta)
                    throw new ArgumentException("La carpeta local ya no existe. Refresque la selección y confirme de nuevo si desea crearla.");
                Directory.CreateDirectory(rutaConfirmada);
                rutaConfirmada = DdlDirectorios.ValidarRutaCreacion(rutaConfirmada);
            }
            var sqlCrearConfirmado = DdlDirectorios.Crear(dialogo.Nombre, rutaConfirmada);
            if (!string.Equals(sqlCrearConfirmado, sentencias[0], StringComparison.Ordinal))
                throw new ArgumentException("La ruta cambió mientras se confirmaba la operación. Revise el SQL y confirme de nuevo.");

            var creado = false;
            try
            {
                await Bd.EjecutarAsync(Modulo.Directorios, "Crear DIRECTORY", sentencias[0]);
                creado = true;
                _creadosEnEstaSesion[dialogo.Nombre] = rutaConfirmada;
                if (!await AdminBdTieneReadWriteDirectoOPublicoAsync(dialogo.Nombre))
                {
                    await Bd.EjecutarAsync(Modulo.Directorios, "Otorgar READ/WRITE a ADMIN_BD", grantAdminBd);
                }
                await Bd.EjecutarAsync(Modulo.Directorios, "Registrar DIRECTORY", SqlDirectorios.Registrar,
                    Parametro.De("nombre", dialogo.Nombre), Parametro.De("ruta", rutaConfirmada));
                Dialogos.Exito(this, $"DIRECTORY {dialogo.Nombre} creado y registrado.");
            }
            catch
            {
                if (creado)
                    Toast.Mostrar(this, $"{dialogo.Nombre} existe en Oracle, pero no terminó de registrarse. Refresque y use «Registrar existente» para reconciliar.", TipoEstado.Advertencia);
                throw;
            }
            finally
            {
                await CargarAsync(dialogo.Nombre);
            }
        });
    }

    private async Task RegistrarAsync()
    {
        if (Seleccionado is not { } seleccionado) return;
        var nombre = Nombre(seleccionado);
        await EjecutarOperacionAsync(async () =>
        {
            var actual = await Bd.ConsultarAsync(SqlDirectorios.DirectorioPorNombre, Parametro.De("nombre", nombre));
            if (actual.Rows.Count == 0) throw new ArgumentException("El DIRECTORY ya no existe en Oracle. Refresque la lista.");
            var fila = actual.Rows[0];
            if (Registrado(fila) && Activo(fila)) throw new ArgumentException("El DIRECTORY ya está registrado y activo. Refresque la lista.");
            if (!await AdminBdTieneReadWriteDirectoOPublicoAsync(nombre))
                throw new ArgumentException($"No se activó {nombre}: ADMIN_BD no tiene grants directos ni PUBLIC de READ y WRITE. Un DIRECTORY externo no recibe grants automáticos desde esta pantalla.");
            var ruta = Ruta(fila);
            var vista = DdlDirectorios.RegistrarVista(nombre, ruta);
            if (!Dialogos.ConfirmarSql(this, $"Registrar {nombre}", vista + ";")) return;
            if (!await AdminBdTieneReadWriteDirectoOPublicoAsync(nombre))
                throw new ArgumentException("Los privilegios de ADMIN_BD cambiaron mientras se confirmaba la operación. Refresque e intente de nuevo.");
            await Bd.EjecutarAsync(Modulo.Directorios, "Registrar DIRECTORY existente", SqlDirectorios.Registrar,
                Parametro.De("nombre", nombre), Parametro.De("ruta", ruta));
            Dialogos.Exito(this, $"DIRECTORY {nombre} registrado y activo para Respaldos.");
            await CargarAsync(nombre);
        });
    }

    private async Task OtorgarAsync()
    {
        if (Seleccionado is not { } seleccionado) return;
        var directorio = Nombre(seleccionado);
        using var dialogo = new DlgOtorgarDirectorio(directorio);
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        await EjecutarOperacionAsync(async () =>
        {
            await VerificarPropiedadAdministrableAsync(directorio);
            var usuario = await Bd.ConsultarAsync(SqlDirectorios.Usuario, Parametro.De("usuario", dialogo.Usuario));
            if (usuario.Rows.Count == 0) throw new ArgumentException($"El usuario {dialogo.Usuario} no existe en Oracle.");
            if (DdlDirectorios.EsUsuarioProtegido(dialogo.Usuario, (string)usuario.Rows[0]["ORACLE_MAINTAINED"], Bd.Usuario))
                throw new ArgumentException("No se pueden otorgar permisos de DIRECTORY a SYS, SYSTEM, usuarios de Oracle ni a la sesión actual.");
            if (((string)usuario.Rows[0]["ACCOUNT_STATUS"]).Contains("LOCKED", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"El usuario {dialogo.Usuario} está bloqueado y no puede recibir permisos desde esta pantalla.");

            var sql = DdlDirectorios.OtorgarLecturaEscritura(directorio, dialogo.Usuario);
            if (!Dialogos.ConfirmarSql(this, $"Otorgar READ/WRITE sobre {directorio}", sql + ";")) return;
            await VerificarPropiedadAdministrableAsync(directorio);
            await Bd.EjecutarAsync(Modulo.Directorios, "Otorgar READ/WRITE DIRECTORY", sql);
            Dialogos.Exito(this, $"READ y WRITE otorgados a {dialogo.Usuario} sobre {directorio}.");
            await CargarPrivilegiosAsync();
        });
    }

    private async Task EliminarAsync()
    {
        if (Seleccionado is not { } seleccionado) return;
        var directorio = Nombre(seleccionado);
        await EjecutarOperacionAsync(async () =>
        {
            var estado = await ValidarEliminacionAsync(directorio);
            var id = Convert.ToInt32(estado["ID_DIRECTORIO"]);
            var sentencias = new[] { DdlDirectorios.DesactivarVista(id), DdlDirectorios.Eliminar(directorio) };
            if (!Dialogos.ConfirmarSql(this, $"Eliminar DIRECTORY {directorio}", string.Join(";\r\n", sentencias) + ";", destructiva: true)) return;

            // La confirmación puede permanecer abierta: se vuelve a consultar justo antes de modificar.
            estado = await ValidarEliminacionAsync(directorio);
            id = Convert.ToInt32(estado["ID_DIRECTORIO"]);
            var filas = await Bd.EjecutarAsync(Modulo.Directorios, "Desactivar DIRECTORY en catálogo", SqlDirectorios.Desactivar, Parametro.De("id", id));
            if (filas != 1) throw new InvalidOperationException("El catálogo cambió mientras se confirmaba la operación. Refresque e intente de nuevo.");
            try
            {
                await Bd.EjecutarAsync(Modulo.Directorios, "Eliminar DIRECTORY", DdlDirectorios.Eliminar(directorio));
                Dialogos.Exito(this, $"DIRECTORY {directorio} eliminado. La carpeta física y el historial se conservaron.");
            }
            catch
            {
                Toast.Mostrar(this, "El DROP falló: el objeto Oracle se conserva, pero el catálogo quedó inactivo por seguridad. Use «Registrar existente» tras revisar el error.", TipoEstado.Advertencia);
                throw;
            }
            finally
            {
                await CargarAsync();
            }
        });
    }

    private async Task<DataRow> ValidarEliminacionAsync(string directorio)
    {
        var tabla = await Bd.ConsultarAsync(SqlDirectorios.EstadoEliminar, Parametro.De("nombre", directorio));
        if (tabla.Rows.Count == 0) throw new ArgumentException("El DIRECTORY ya no existe en Oracle. Refresque la lista.");
        var fila = tabla.Rows[0];
        if (!EsPropiedadVerificada(fila))
            throw new ArgumentException("Este DIRECTORY es de solo lectura: la aplicación no puede verificar que haya sido creado por esta sesión. Los objetos existentes o internos no admiten GRANT ni DROP.");
        if ((fila["ACTIVO"] as string) != "S")
            throw new ArgumentException("El DIRECTORY ya está inactivo en el catálogo. Refresque la lista.");
        var dependencias = new DependenciasEliminacion(
            Convert.ToInt32(fila["RESPALDOS_RESTAURABLES"]),
            Convert.ToInt32(fila["RESTAURACIONES_ACTIVAS"]),
            Convert.ToInt32(fila["PROGRAMACIONES_ACTIVAS"]),
            Convert.ToInt32(fila["JOBS_EJECUTANDO"]));
        if (dependencias.ImpideEliminar)
            throw new ArgumentException($"No se puede eliminar: respaldos no eliminados={dependencias.RespaldosRestaurables}, restauraciones activas={dependencias.RestauracionesActivas}, programaciones activas={dependencias.ProgramacionesActivas}, jobs Data Pump ejecutando={dependencias.JobsEjecutando}.");
        return fila;
    }

    /// <summary>Comprueba grants directos o PUBLIC; no pretende resolver privilegios heredados por roles.</summary>
    private async Task<bool> AdminBdTieneReadWriteDirectoOPublicoAsync(string directorio)
    {
        var privilegios = await Bd.ConsultarAsync(SqlDirectorios.PrivilegiosDirectosOPublicosAdminBd, Parametro.De("directorio", directorio));
        var encontrados = privilegios.Rows.Cast<DataRow>().Select(f => (string)f["PRIVILEGE"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return encontrados.Contains("READ") && encontrados.Contains("WRITE");
    }

    private bool EsPropiedadVerificada(DataRow fila)
    {
        if (fila["ID_DIRECTORIO"] is DBNull || fila["RUTA_CATALOGO"] is not string rutaCatalogo) return false;
        var nombre = Nombre(fila);
        if (!DdlDirectorios.EsNombreDemostracionPermitido(nombre) ||
            !_creadosEnEstaSesion.TryGetValue(nombre, out var rutaCreada)) return false;
        return string.Equals(rutaCreada, Ruta(fila), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(rutaCreada, rutaCatalogo, StringComparison.OrdinalIgnoreCase);
    }

    private async Task VerificarPropiedadAdministrableAsync(string directorio)
    {
        var tabla = await Bd.ConsultarAsync(SqlDirectorios.DirectorioPorNombre, Parametro.De("nombre", directorio));
        if (tabla.Rows.Count != 1 || !Activo(tabla.Rows[0]) || !EsPropiedadVerificada(tabla.Rows[0]))
            throw new ArgumentException("El DIRECTORY dejó de ser administrable: su proveniencia de esta sesión, ruta Oracle o registro de catálogo no coinciden.");
    }

    private async Task EjecutarOperacionAsync(Func<Task> accion)
    {
        if (_ocupado) return;
        _ocupado = true;
        ActualizarEstadoInteraccion();
        try { await accion(); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
        finally
        {
            _ocupado = false;
            ActualizarEstadoInteraccion();
        }
    }

    private void ActualizarEstadoInteraccion()
    {
        UseWaitCursor = _ocupado || _cargando || _cargandoPrivilegios;
        ActualizarBotones();
    }
}

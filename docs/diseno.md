# Documento de diseño — OraAdmin XE

> Basado en la propuesta original del equipo (@Youfrend, 3 oct 2026), ajustada el 26 oct 2026: stack C# + WinForms, calendario de una semana y modelo 3FN completo.

## 1. Intención

El enunciado pide «una aplicación para la administración y ejecución de respaldos» sobre **Oracle 21c XE**, que se presenta en el laboratorio el **lunes 2 de noviembre de 2026**. El 70 % de la nota es **funcionalidad**. Por eso la regla de oro del proyecto es esta:

> **Un módulo sencillo que ejecuta cosas reales en la BD vale más que uno bonito que no hace nada.** Primero tienen que funcionar los 8 módulos de punta a punta. Después se pule.

### Rúbrica

| Rubro | Peso | Qué significa para nosotros |
|---|---|---|
| Funcionalidad | 70 % | Cada módulo hace cosas reales frente al profe, sin caerse |
| Ventanas o pantallas | 20 % | Pantallas ordenadas, consistentes (mismo `Tema`) y fáciles de usar |
| Manual de usuario | 10 % | Máximo 4 páginas, con capturas |

### Objetivos

- Cubrir **todos** los puntos del enunciado: tablespaces; tuning (planes, estadísticas, índices); performance; auditoría; seguridad de usuarios; respaldos por esquema, por tabla y full; recuperación; y administración de archivos de respaldo y directorios.
- Dejar un catálogo propio de respaldos y restauraciones, más una bitácora de todo lo ejecutado, en un **modelo en 3FN**.
- Preparar una demo de unos 15 minutos que sea a prueba de fallos (ver [`guion-demo.md`](guion-demo.md)).

### Fuera de alcance

- Administrar varias bases a la vez o conectarse a la CDB raíz (`XE`). Se trabaja **solo** en la PDB `XEPDB1`.
- RMAN. El enunciado habla de respaldos lógicos por esquema y por tabla, y eso es Data Pump.
- Instalador, multiplataforma o modo web.
- Usuarios propios de la app. El login es el usuario de Oracle.

### Lo que lo haría fracasar aunque «funcione»

- Que la app se congele durante un respaldo.
- Que un error muestre «Error» sin el `ORA-xxxxx`.
- Que se pueda borrar `SYS`, `SYSTEM` u otro usuario de Oracle por accidente.
- Que funcione en una máquina y no en la del laboratorio.

## 2. Decisiones tomadas

| Tema | Decisión | Por qué |
|---|---|---|
| Lenguaje y UI | **C# 12 + .NET 8 + WinForms** en Visual Studio 2022 | Ventanas de escritorio con un diseñador visual muy rápido |
| Conexión | `Oracle.ManagedDataAccess.Core` (NuGet), Easy Connect `localhost:1521/XEPDB1` | No requiere instalar Oracle Client |
| Contenedor | PDB **`XEPDB1`**, nunca `XE` (CDB$ROOT) | En la raíz los usuarios necesitan el prefijo `C##` |
| Usuario de la app | **`ADMIN_BD`** con rol DBA y privilegios directos de Data Pump y `UTL_FILE`; es dueño del modelo | Data Pump no debe correrse como SYSDBA |
| Respaldos | **`DBMS_DATAPUMP`** en bloques anónimos enviados desde la app | Todo va por la misma conexión, se puede leer el estado del job y no depende de `expdp.exe` |
| Archivos en disco | `System.IO` sobre la ruta del `DIRECTORY` (la app corre en la misma máquina que la BD), y `UTL_FILE` para borrar y renombrar | Es el caso del laboratorio |
| Auditoría | **Auditoría unificada** (`UNIFIED_AUDIT_TRAIL`) | La auditoría tradicional está en desuso en 21c |
| Gráficos | `ScottPlot.WinForms` | `System.Windows.Forms.DataVisualization` no existe en .NET 8 |
| Modelo propio | 13 tablas en **3FN** (ver [`modelo-datos.md`](modelo-datos.md)) | Catálogo de respaldos, bitácora e historial de rendimiento |
| Datos de prueba | Esquema `DEMO_VENTAS` con 252 500 filas | Para que el tuning muestre diferencias reales y haya algo que respaldar y restaurar |

## 3. Pantallas

| # | Pantalla | Qué hace el usuario ahí |
|:-:|---|---|
| 0 | Login | Host, puerto (1521), servicio (XEPDB1), usuario y contraseña. Valida que el usuario tenga rol DBA y recuerda todo menos la contraseña |
| 1 | Inicio | Estado de la instancia, versión, uptime, sesiones activas, % usado de cada tablespace (semáforo), último respaldo y su estado |
| 2 | Tablespaces | Listar con % usado. Crear, agregar o redimensionar datafiles, autoextend, online/offline, read only/read write y eliminar |
| 3 | Tuning | Escribir una consulta, ver su plan (estimado y real), recolectar estadísticas, crear, eliminar o reconstruir índices y comparar el plan antes y después, lado a lado |
| 4 | Performance | Sesiones activas, bloqueos (quién bloquea a quién), top SQL, eventos de espera, SGA/PGA y gráfico con refresco automático. Matar sesiones |
| 5 | Auditoría | Crear, activar y desactivar políticas de auditoría unificada. Consultar la pista con filtros y exportarla a CSV |
| 6 | Seguridad | Usuarios, roles, privilegios y perfiles: crear, bloquear y desbloquear, expirar contraseña, otorgar y revocar |
| 7 | Respaldos | Asistente: tipo (Full / Esquema / Tabla), objetos, directorio y nombre de archivo. Ejecutar con progreso y log, y ver el catálogo |
| 8 | Recuperación | Elegir un respaldo del catálogo, qué restaurar, remapear el esquema y qué hacer si la tabla ya existe |
| 9 | Directorios y archivos | Crear y eliminar objetos `DIRECTORY` y dar permisos. Listar los `.dmp`/`.log` con tamaño y fecha, verlos, borrarlos o renombrarlos y cruzarlos con el catálogo |
| 10 | Bitácora | Historial de acciones con su SQL, su resultado y filtros |

**Layout común:** menú lateral oscuro a la izquierda (`FrmPrincipal`) y, en el contenido, el título del módulo arriba, una barra de acciones (botones) y la grilla o grillas de resultados. Para varias vistas dentro de un módulo se usan pestañas (`TabControl`), como en Seguridad: Usuarios, Roles, Privilegios y Perfiles.

## 4. Reglas comunes para todas las pantallas

1. **Botón «Ver SQL».** Toda acción muestra la sentencia exacta antes de ejecutarla (`Dialogos.ConfirmarSql`). Al profe le demuestra que entendemos lo que pasa por debajo.
2. **Confirmación antes de toda acción destructiva:** DROP, KILL SESSION, borrar un archivo, restaurar con REPLACE o TRUNCATE, REVOKE. Se usa `ConfirmarSql(..., destructiva: true)`.
3. **Errores de Oracle completos.** Siempre el código `ORA-xxxxx` con su texto (`Dialogos.Error`), nunca un «Error» genérico.
4. **Todo queda en la bitácora.** `Bd.EjecutarAsync` registra automáticamente. Las acciones largas (respaldos) usan `Bd.RegistrarBitacoraAsync`.
5. **Proteger lo de Oracle.** Las listas de usuarios, roles y esquemas filtran `ORACLE_MAINTAINED = 'N'` por defecto (con una casilla «Mostrar objetos de Oracle»). Los tablespaces `SYSTEM`, `SYSAUX`, `UNDOTBS1` y `TEMP` no se pueden borrar ni poner offline desde la app.
6. **Nada de concatenar valores del usuario.** Los valores van con variables bind (`:nombre`). Los nombres de objetos, que no admiten bind, pasan por `Identificador.Validar`.
7. **Las operaciones largas no congelan la pantalla.** Todo acceso a la BD es `async`/`await`. Mientras corre, se deshabilitan los botones y se muestra el cursor de espera o una barra de progreso.
8. **Usar el sistema visual** (sección 6): colores de `Tema`, `BotonPlano`, pastillas de estado y estados vacíos. Nada de `MessageBox` ni colores sueltos.

## 5. Contrato de código (para trabajar en paralelo)

Cada pantalla es un `UserControl` en `Modulos/<Módulo>/` que hereda de `UI/ModuloBase`. El menú se arma solo desde `Modulos/RegistroModulos.cs`. Así cada persona toca **sus** archivos y no hay conflictos.

```csharp
public partial class UcTablespaces : ModuloBase
{
    public override Modulo Modulo => Modulo.Tablespaces;

    // Se llama cada vez que el usuario abre la pantalla
    public override async Task AlActivarAsync() => await CargarAsync();

    private async Task CargarAsync()
    {
        try { grdTablespaces.DataSource = await Bd.ConsultarAsync(SqlUso); }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }

    private async void BtnCrear_Click(object? sender, EventArgs e)
    {
        try
        {
            var nombre = Identificador.Validar(txtNombre.Text, "tablespace");
            var sql = $"CREATE TABLESPACE {nombre} DATAFILE {Identificador.Literal(ruta)} SIZE {tamano}M AUTOEXTEND ON";
            if (!Dialogos.ConfirmarSql(this, "Crear tablespace", sql)) return;

            await Bd.EjecutarAsync(Modulo, "Crear tablespace", sql);   // registra en la bitácora
            Dialogos.Exito(this, $"Tablespace {nombre} creado.");
            await CargarAsync();
        }
        catch (Exception ex) { Dialogos.Error(this, ex); }
    }
}
```

| Pieza | Archivo | Estado |
|---|---|---|
| `IBaseDatos` (contrato) | `Datos/IBaseDatos.cs` | ✅ definido |
| `BaseDatos` (implementación) | `Datos/BaseDatos.cs` | ✅ listo (#3) |
| `Identificador` | `Datos/Identificador.cs` | ✅ listo |
| `Dialogos` + `DlgSql` + `Csv` | `UI/` | ✅ listo (#4) |
| `FrmLogin` | `UI/FrmLogin.cs` | ✅ listo (#5) |
| `FrmPrincipal` + menú | `UI/FrmPrincipal.cs` | ✅ listo |
| `Tema` | `UI/Tema.cs` | ✅ listo |
| Pantallas | `Modulos/*/Uc*.cs` | ⏳ una issue por módulo |

**Mientras el Núcleo no esté listo** (martes 27, issues #3 y #4), cada persona puede diseñar sus pantallas en el diseñador de Visual Studio y probar sus consultas en SQL Developer: el contrato ya está definido.

## 6. Sistema visual (tema oscuro «Medianoche violeta», issue #38)

La app es **oscura**: fondo `#0E1016`, menú `#0A0B10`, superficies `#14161F` y acento violeta `#7C6CF2`. **No hay que pintar nada a mano**: `Tema.Aplicar` recorre cada pantalla y le pone el tema. Lo que se diseñe en Visual Studio con los colores por defecto se ve oscuro al correr la app (en el diseñador se sigue viendo claro, y eso es normal).

| Necesito… | Uso | Ejemplo |
|---|---|---|
| Un botón | `BotonPlano` (en el diseñador está en el cuadro de herramientas) o `Tema.Boton(...)` | `Tema.Boton("Crear tablespace", primario: true, icono: "")` |
| Botón destructivo | `Estilo = EstiloBoton.Peligro` | Eliminar, Revocar, Matar sesión |
| Un diálogo o formulario nuevo | heredar de **`VentanaBase`** (no de `Form`) | `public partial class DlgTablespace : VentanaBase` |
| Pestañas | `Pestanas` (el `TabControl` de Windows no admite oscuro) | `pestanas.Agregar("Datafiles", new UcDatafiles());` |
| Estado en una grilla (OPEN, ERROR, ONLINE…) | `Tema.ColumnaEstado(grilla, "COLUMNA")` | Lo pinta como pastilla verde, amarilla, roja o gris según el texto |
| Grilla vacía | `Tema.MensajeVacio(grilla, "No hay respaldos todavía", "")` | Ícono y mensaje centrados |
| «Cargando…» | `Tema.Cargando(grilla, true)` / `false` | Mientras corre la consulta |
| Aviso de éxito | `Dialogos.Exito(this, "Respaldo creado")` | **Toast** abajo a la derecha que se va solo |
| Tarjeta de indicador | `Modulos/Dashboard/Tarjeta` | Ver Inicio |

**Íconos:** glifos de la fuente *Segoe MDL2 Assets* (`` agregar, `` borrar, `` refrescar, `` guardar, `` candado, `` carpeta, `` gráfico). La lista completa está en la documentación de Microsoft: *Segoe MDL2 Assets icons*.

**Límites conocidos de WinForms en oscuro:** las casillas dentro de un `CheckedListBox`, las flechas de `NumericUpDown` y el calendario del `DateTimePicker` siguen siendo claros, porque son controles nativos.

## 7. Trampas conocidas (leer antes de empezar)

- **Conectarse a `XEPDB1`, no a `XE`.**
- **Oracle no crea la carpeta del `DIRECTORY`.** Hay que crearla en el disco antes. Si sale `ORA-39070` u `ORA-29283`, es un problema de ruta o de permisos del servicio `OracleServiceXE` sobre la carpeta.
- **Los roles no aplican dentro de procedimientos almacenados normales.** Por eso todo se manda como bloques anónimos desde la app y `ADMIN_BD` tiene grants directos sobre `DBMS_DATAPUMP` y `UTL_FILE`.
- **`DISPLAY_CURSOR(NULL, NULL)` lee el último SQL de la sesión.** La consulta y el `DISPLAY_CURSOR` tienen que ir por la **misma conexión**, con `Bd.EnMismaSesionAsync`, porque el pool de ODP.NET puede dar otra.
- **Data Pump pasa a mayúsculas el nombre del `.dmp`** (`demo.dmp` → `DEMO.DMP`), pero no el del `.log`. En Windows hay que comparar los nombres sin distinguir mayúsculas.
- **Nombres de job únicos.** Usar `EXP_<TIPO>_<yyyyMMdd_HHmmss>`. Si un job falla, puede quedar su tabla maestra. `DBA_DATAPUMP_JOBS` con estado `NOT RUNNING` se limpia con `DROP TABLE ADMIN_BD.<job> PURGE`.
- **`PASSWORD_VERIFY_FUNCTION ora12c_verify_function`** falla si antes no se corrió `utlpwdmg.sql` en la PDB. Para la demo no se usa.
- **`ALTER SYSTEM KILL SESSION`** necesita `'sid,serial#'`. No se debe matar la propia sesión de la app (`SYS_CONTEXT('USERENV','SID')`).
- **`new OracleParameter("x", 0)` no pasa el valor 0:** C# convierte el literal `0` a `OracleDbType` y llama al constructor equivocado (`ArgumentOutOfRangeException`). Usar siempre **`Parametro.De("x", valor)`**.
- **Dentro de la PDB, `V$SYSMETRIC` viene vacía.** Para las métricas de Performance se usa `V$CON_SYSMETRIC` (`group_id = 18`).
- **Para la demo de tuning no se usa `COUNT(*)`**, porque Oracle lo resuelve con el índice de la PK. Hay que consultar columnas de la tabla (`SELECT *` o `SUM(cantidad)`).
- **Límites de XE:** 2 hilos de CPU, 2 GB de RAM y 12 GB de datos de usuario. El respaldo **full** de la PDB tarda un par de minutos y genera cientos de MB.
- **Las contraseñas no van al repo.** `conexion.json` está en el `.gitignore` y no guarda la contraseña.
- **Probar en una máquina del laboratorio antes del domingo.** Si no tiene Visual Studio, se lleva el `.exe` publicado con `dotnet publish -c Release -r win-x64 --self-contained`.

## 8. Extras (solo si lo obligatorio ya funciona)

Respaldos programados con `DBMS_SCHEDULER` (tablas `PROGRAMACION*`), política de retención («borrar respaldos con más de N días»), exportar cualquier grilla a CSV, semáforos en Inicio y comparador de planes lado a lado. Cada uno tiene su issue con la etiqueta `extra`.

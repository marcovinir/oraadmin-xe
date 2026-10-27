# Cómo trabajamos

Somos 4 personas tocando la misma solución durante una semana. Estas reglas existen para que **nadie pise el trabajo de otro** y para que `main` siempre compile.

## Reglas del equipo

1. **Primero SQL Developer, después la app.** Nadie programa una pantalla sin haber probado antes sus consultas como `ADMIN_BD@XEPDB1`. Las consultas base están en [`docs/referencia-sql.md`](docs/referencia-sql.md).
2. **Toda llamada a Oracle pasa por `Bd`** (`IBaseDatos`). Así la bitácora, los errores ORA y la conexión funcionan igual en todo lado. No se crean `OracleConnection` sueltas.
3. **Cada quien trabaja en su carpeta**: `src/OraAdminXE/Modulos/<SuMódulo>/`. Si hace falta tocar algo compartido (`Datos/`, `UI/`, el `.csproj`), se avisa en el grupo y se hace en un PR aparte y pequeño.
4. **Cada Pull Request lo revisa otra persona** antes de unirlo a `main`.
5. **Cada quien escribe la sección del manual de sus módulos** y entrega 1 o 2 capturas.
6. **Nunca se suben** `.dmp`, `.log`, `conexion.json` ni contraseñas.

## Flujo diario

```bash
git checkout main
git pull origin main
git checkout -b feature/respaldos      # una rama por issue o por módulo
# ... trabajar, probar con F5 ...
git add src/OraAdminXE/Modulos/Respaldos
git commit -m "Respaldos: export por esquema con DBMS_DATAPUMP (#12)"
git push -u origin feature/respaldos
gh pr create --fill                    # o desde la web de GitHub
```

Cuando el PR se une, se borra la rama y se cierra la issue (`Closes #12` en la descripción del PR la cierra sola).

### Ramas

| Rama | Para qué |
|---|---|
| `main` | Siempre compila y corre. Solo entra código por PR |
| `feature/<módulo>-<algo>` | Trabajo de una issue: `feature/nucleo-datos`, `feature/respaldos`, `feature/tuning-planes` |
| `fix/<algo>` | Corrección de un error: `fix/login-ora12514` |
| `docs/<algo>` | Manual, capturas, README |

### Commits

Se escriben en español, en presente, con el módulo al inicio y la issue al final:

```text
Tablespaces: crear tablespace con autoextend (#8)
Núcleo: diálogo Ver SQL con botón Copiar (#3)
Manual: sección de respaldos y recuperación (#27)
```

### Pull Requests

- Deben ser **pequeños** (una issue) y llevar una captura si cambian una pantalla.
- Hay que llenar la plantilla, que incluye una lista de verificación.
- **Antes de pedir revisión:** `main` actualizado y fusionado en la rama, compila sin errores y se probó con F5 contra Oracle.

## Convenciones de código (C#)

| Qué | Convención | Ejemplo |
|---|---|---|
| Pantallas de módulo | `Uc<Módulo>` en `Modulos/<Módulo>/` | `UcRespaldos` |
| Formularios o diálogos | `Frm<Nombre>` / `Dlg<Nombre>` | `FrmLogin`, `DlgNuevoUsuario` |
| Controles | prefijo de tipo + nombre en PascalCase | `btnCrear`, `txtNombre`, `grdTablespaces`, `cboTipo`, `chkAutoextend`, `numTamano`, `tabSeguridad` |
| Campos privados | `_camelCase` | `_temporizador` |
| Métodos async | sufijo `Async` | `CargarAsync()` |
| SQL | constantes `const string` al inicio de la clase o en un archivo `Sql<Módulo>.cs` en la carpeta del módulo | `SqlTablespaces.Uso` |
| Idioma | Español para nombres del dominio y textos de la UI | `CrearRespaldoAsync` |

**Patrón de un botón de acción** (ver [`docs/diseno.md`](docs/diseno.md#5-contrato-de-código-para-trabajar-en-paralelo)):

1. Validar los nombres con `Identificador.Validar`.
2. Armar el SQL.
3. Pedir confirmación con `Dialogos.ConfirmarSql` (`destructiva: true` si borra algo).
4. Ejecutar con `await Bd.EjecutarAsync(Modulo, "acción", sql)`.
5. Mostrar el éxito y recargar.
6. Mostrar cualquier error con `catch (Exception ex) { Dialogos.Error(this, ex); }`.

## Issues

- La issue **#1 · Hoja de ruta** es el punto de entrada: el orden, los milestones y quién hace qué.
- Cada issue es **autosuficiente**: contexto, qué hacer, archivos, SQL de referencia y criterios de aceptación.
- **Antes de empezar una issue, asígnatela** (*Assignees* → tu usuario) y muévela a *En progreso* si usamos el tablero.
- Las etiquetas `persona-N` indican a quién le toca. Las `mod: …` indican el módulo y `prioridad: alta` lo que va primero.
- Si encuentras un error o una idea nueva, abre una issue con la plantilla correspondiente. No lo dejes solo en el chat del grupo.

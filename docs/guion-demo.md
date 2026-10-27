# Guion de la presentación

Es una historia de **~15 minutos** en la que cada módulo hace algo visible. Hay que **ensayarla al menos dos veces** en una máquina del laboratorio (o en una recién preparada) antes del lunes 2 de noviembre.

## Antes de entrar al laboratorio

- [ ] Llevar el `.exe` publicado (release `v1.0` de GitHub) en una llave USB, por si la máquina no tiene Visual Studio.
- [ ] Llevar este repo clonado para correr los scripts `sql/`.
- [ ] Crear `C:\respaldos_bd` y correr `00`, `01` y `02` (ver el README).
- [ ] Tener abiertas **dos ventanas de SQL Developer** conectadas como `DEMO_VENTAS`, para la demo de bloqueo.
- [ ] Tener ya hecho un **respaldo del esquema `DEMO_VENTAS`**, por si el paso 7 falla (plan B).
- [ ] Conectar el cargador de la laptop y desactivar las notificaciones.

## Guion

| # | Quién | Módulo | Qué se muestra | Tiempo |
|:-:|---|---|---|:-:|
| 1 | P1 | Login → Inicio | Login con `ADMIN_BD` sobre `XEPDB1` (y un intento con un usuario sin DBA, que se rechaza). Dashboard: instancia, sesiones, semáforo de tablespaces | 1 min |
| 2 | P3 | Tablespaces | Crear `TS_VENTAS` (50 MB, autoextend), agregarle un datafile, ver el % usado y ponerlo READ ONLY y luego READ WRITE. Botón **Ver SQL** | 1,5 min |
| 3 | P1 | Seguridad | Crear el perfil `PERFIL_DEMO` (3 intentos y 1 minuto de bloqueo), el usuario `JUAN` con ese perfil y cuota en `TS_VENTAS`, y el rol `ROL_CONSULTA` (`SELECT` sobre `FACTURA`) asignado a `JUAN` | 2 min |
| 4 | P3 | Auditoría | Activar `ORA_LOGON_FAILURES` y crear y activar `POL_DEMO_DML` sobre `DEMO_VENTAS.FACTURA`. Fallar 3 veces el login de `JUAN` → en Seguridad aparece `LOCKED(TIMED)` → en la pista de auditoría se ven los intentos → desbloquearlo | 2 min |
| 5 | P4 | Tuning | `SELECT * FROM demo_ventas.detalle_factura WHERE id_producto = 25` → plan con **TABLE ACCESS FULL (costo 173)** → crear índice y recolectar estadísticas → **INDEX RANGE SCAN (costo 107)**, lado a lado | 2 min |
| 6 | P4 | Performance | Gráfico en vivo. En SQL Developer, la sesión A hace `UPDATE demo_ventas.cliente SET nombre = nombre WHERE id_cliente = 1` sin COMMIT y la B hace lo mismo y se queda esperando. La app muestra quién bloquea a quién → **matar la sesión A** → la B continúa | 2 min |
| 7 | P2 | Respaldos | Respaldo **por tabla** (`FACTURA`), **por esquema** (`DEMO_VENTAS`) y **full**, con progreso y log. Ver el catálogo | 2 min |
| 8 | P2 | Recuperación | `DROP TABLE demo_ventas.detalle_factura PURGE` → `COUNT(*)` da error → restaurar **solo** `DETALLE_FACTURA` desde el respaldo del esquema → `COUNT(*)` = **200 000**. Restaurar el esquema completo como `DEMO_COPIA` (`REMAP_SCHEMA`) | 2 min |
| 9 | P3 | Directorios y archivos | Crear `DIR_RESPALDOS2`, ver los `.dmp`/`.log` con tamaño y fecha, abrir un log y borrar un respaldo viejo (pasa a `ELIMINADO` en el catálogo) | 1 min |
| 10 | P1 | Bitácora | Todo lo que se hizo durante la demo, con su SQL y su resultado (filtrar por módulo) | 0,5 min |

## Plan B

| Si falla… | Hacer |
|---|---|
| La app no abre en la máquina del lab | Usar el `.exe` *self-contained* de la USB o la laptop de un integrante |
| El respaldo full tarda mucho | Mostrar el progreso y seguir con Recuperación usando el respaldo ya hecho del esquema |
| `ORA-39070` / `ORA-29283` (directorio) | Crear la carpeta y reintentar, o usar `DATA_PUMP_DIR`, que ya existe |
| Algo quedó a medias entre ensayos | `sqlplus / as sysdba @sql/99_limpieza.sql` y volver a correr `00`, `01` y `02` (~2 min) |
| Todo lo demás | Capturas o un video corto de cada módulo en `docs/capturas/` |

## Limpieza después de cada ensayo

Desde la app: borrar `TS_VENTAS`, `JUAN`, `ROL_CONSULTA`, `PERFIL_DEMO`, `POL_DEMO_DML`, `DIR_RESPALDOS2` y el índice `IX_DET_PRODUCTO`. Si no da tiempo, se corre `99_limpieza.sql` y la reinstalación.

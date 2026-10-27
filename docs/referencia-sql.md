# Referencia SQL por módulo

Cada módulo se reduce a **consultar vistas del diccionario** y **ejecutar unas pocas sentencias**. Primero se prueban en SQL Developer conectado como `ADMIN_BD@XEPDB1` y después se copian a la app.

> Las consultas de lectura de este documento se probaron en Oracle 21c XE (21.3) sobre `XEPDB1`. Donde dice `:nombre` va una variable bind. Donde dice `{NOMBRE}` va un identificador validado con `Identificador.Validar`.

---

## 0. Login y validación de privilegios

```sql
-- ¿El usuario conectado es DBA?
SELECT COUNT(*) FROM session_roles WHERE role = 'DBA';
-- Datos de la sesión
SELECT SYS_CONTEXT('USERENV','SESSION_USER')  usuario,
       SYS_CONTEXT('USERENV','CON_NAME')      contenedor,
       SYS_CONTEXT('USERENV','SID')           sid
FROM dual;
```

## 1. Inicio (dashboard)

```sql
SELECT instance_name, host_name, version_full, startup_time, status,
       ROUND(SYSDATE - startup_time, 2) AS dias_arriba
FROM v$instance;

SELECT COUNT(*) FROM v$session WHERE type = 'USER' AND status = 'ACTIVE';

-- Último respaldo (modelo propio)
SELECT * FROM v_respaldos ORDER BY fecha_inicio DESC FETCH FIRST 1 ROW ONLY;
```

## 2. Tablespaces

Vistas: `DBA_TABLESPACES`, `DBA_DATA_FILES`, `DBA_FREE_SPACE`, `DBA_TEMP_FILES`, `DBA_SEGMENTS`.

```sql
-- Uso por tablespace (permanentes)
SELECT t.tablespace_name, t.status, t.contents,
       ROUND(df.total / 1048576)                       AS total_mb,
       ROUND((df.total - NVL(fs.libre, 0)) / 1048576)  AS usado_mb,
       ROUND((df.total - NVL(fs.libre, 0)) / df.total * 100, 1) AS pct_usado,
       df.autoextend
FROM dba_tablespaces t
JOIN (SELECT tablespace_name, SUM(bytes) total,
             MAX(autoextensible) autoextend
      FROM dba_data_files GROUP BY tablespace_name) df ON df.tablespace_name = t.tablespace_name
LEFT JOIN (SELECT tablespace_name, SUM(bytes) libre
           FROM dba_free_space GROUP BY tablespace_name) fs ON fs.tablespace_name = t.tablespace_name
ORDER BY pct_usado DESC;

-- Datafiles de un tablespace
SELECT file_id, file_name, ROUND(bytes/1048576) mb, autoextensible,
       ROUND(maxbytes/1048576) max_mb, status
FROM dba_data_files WHERE tablespace_name = :ts;

-- Carpeta donde están los datafiles de la PDB (para proponer la ruta del nuevo)
SELECT SUBSTR(file_name, 1, INSTR(file_name, '\', -1)) AS carpeta
FROM dba_data_files WHERE tablespace_name = 'USERS';
```

```sql
CREATE TABLESPACE {TS} DATAFILE 'C:\...\XEPDB1\{TS}01.DBF' SIZE 50M AUTOEXTEND ON NEXT 10M MAXSIZE 500M;
ALTER TABLESPACE {TS} ADD DATAFILE 'C:\...\XEPDB1\{TS}02.DBF' SIZE 20M;
ALTER DATABASE DATAFILE 'C:\...\{TS}01.DBF' RESIZE 100M;
ALTER DATABASE DATAFILE 'C:\...\{TS}01.DBF' AUTOEXTEND ON NEXT 10M MAXSIZE 1G;   -- u OFF
ALTER TABLESPACE {TS} OFFLINE;          -- ONLINE · READ ONLY · READ WRITE
DROP TABLESPACE {TS} INCLUDING CONTENTS AND DATAFILES;
```

Protegidos (no se ofrecen DROP ni OFFLINE): `SYSTEM`, `SYSAUX`, `UNDOTBS1`, `TEMP`. XE permite **12 GB** de datos de usuario en total.

## 3. Tuning de consultas

Vistas y paquetes: `EXPLAIN PLAN`, `DBMS_XPLAN`, `DBMS_STATS`, `DBA_TAB_STATISTICS`, `DBA_INDEXES`, `DBA_IND_COLUMNS`, `V$SQLAREA`.

```sql
-- Plan ESTIMADO
EXPLAIN PLAN SET STATEMENT_ID = 'OAX_1' FOR
  SELECT * FROM demo_ventas.detalle_factura WHERE id_producto = 25;
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY('PLAN_TABLE', 'OAX_1', 'TYPICAL'));
DELETE FROM plan_table WHERE statement_id = 'OAX_1';

-- Plan REAL (las dos sentencias en la MISMA sesión: Bd.EnMismaSesionAsync)
-- Ojo: con COUNT(*) Oracle usa el índice de la PK (INDEX FAST FULL SCAN); para la demo usar columnas de la tabla
SELECT /*+ GATHER_PLAN_STATISTICS */ SUM(cantidad * precio_unitario) FROM demo_ventas.detalle_factura WHERE id_producto = 25;
SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST'));

-- Costo total del plan (para comparar antes/después)
SELECT cost FROM plan_table WHERE statement_id = 'OAX_1' AND id = 0;
```

```sql
-- Estadísticas
BEGIN DBMS_STATS.GATHER_TABLE_STATS('DEMO_VENTAS', 'DETALLE_FACTURA', cascade => TRUE); END;
BEGIN DBMS_STATS.GATHER_SCHEMA_STATS('DEMO_VENTAS'); END;
BEGIN DBMS_STATS.DELETE_TABLE_STATS('DEMO_VENTAS', 'DETALLE_FACTURA'); END;
SELECT table_name, num_rows, blocks, last_analyzed, stale_stats
FROM dba_tab_statistics WHERE owner = :esquema AND object_type = 'TABLE';

-- Índices
SELECT i.index_name, i.table_name, i.uniqueness, i.status, i.blevel, i.distinct_keys,
       LISTAGG(c.column_name, ', ') WITHIN GROUP (ORDER BY c.column_position) columnas
FROM dba_indexes i JOIN dba_ind_columns c ON c.index_owner = i.owner AND c.index_name = i.index_name
WHERE i.owner = :esquema
GROUP BY i.index_name, i.table_name, i.uniqueness, i.status, i.blevel, i.distinct_keys;

CREATE INDEX DEMO_VENTAS.IX_DET_PRODUCTO ON DEMO_VENTAS.DETALLE_FACTURA(ID_PRODUCTO);
ALTER INDEX DEMO_VENTAS.IX_DET_PRODUCTO REBUILD;
DROP INDEX DEMO_VENTAS.IX_DET_PRODUCTO;

-- Top 10 SQL por tiempo
SELECT sql_id, executions, ROUND(elapsed_time/1e6, 2) seg, buffer_gets, disk_reads,
       SUBSTR(sql_text, 1, 100) texto
FROM v$sqlarea ORDER BY elapsed_time DESC FETCH FIRST 10 ROWS ONLY;
```

**Pantalla estrella:** el plan antes (`TABLE ACCESS FULL`, costo alto) → crear índice y recolectar estadísticas → el plan después (`INDEX RANGE SCAN`, costo bajo), uno al lado del otro.

✅ Probado en XE 21.3 con `SELECT * FROM demo_ventas.detalle_factura WHERE id_producto = 25` (404 filas). Sin índice: `TABLE ACCESS FULL`, **costo 173**. Con `IX_DET_PRODUCTO`: `INDEX RANGE SCAN` + `TABLE ACCESS BY INDEX ROWID BATCHED`, **costo 107**.

## 4. Performance

Vistas: `V$SESSION`, `V$LOCK`, `V$LOCKED_OBJECT`, `V$CON_SYSMETRIC`, `V$SYSTEM_EVENT`, `V$SGAINFO`, `V$PGASTAT`, `V$SESSION_LONGOPS`.

```sql
-- Sesiones de usuario y quién bloquea a quién
SELECT sid, serial#, username, status, machine, program, sql_id,
       blocking_session, event, seconds_in_wait
FROM v$session WHERE type = 'USER' ORDER BY blocking_session NULLS LAST, sid;

-- Objetos bloqueados
SELECT lo.session_id sid, o.owner, o.object_name, lo.locked_mode
FROM v$locked_object lo JOIN dba_objects o ON o.object_id = lo.object_id;

-- Métricas del último minuto de la PDB
-- ⚠️ Dentro de XEPDB1, V$SYSMETRIC viene VACÍA: usar V$CON_SYSMETRIC (group_id 18 = intervalo de 60 s)
SELECT metric_name, ROUND(value, 2) valor, metric_unit
FROM v$con_sysmetric
WHERE group_id = 18
  AND metric_name IN ('Average Active Sessions', 'CPU Usage Per Sec', 'Logical Reads Per Sec',
                      'Executions Per Sec', 'User Transaction Per Sec');

-- Top eventos de espera
SELECT event, wait_class, total_waits, ROUND(time_waited_micro/1e6) seg
FROM v$system_event WHERE wait_class <> 'Idle'
ORDER BY time_waited_micro DESC FETCH FIRST 10 ROWS ONLY;

-- Memoria
SELECT name, ROUND(bytes/1048576) mb, resizeable FROM v$sgainfo;
SELECT name, value, unit FROM v$pgastat
WHERE name IN ('total PGA allocated', 'total PGA inuse', 'aggregate PGA target parameter');

-- Matar sesión (confirmación; nunca la propia: SYS_CONTEXT('USERENV','SID'))
ALTER SYSTEM KILL SESSION '{sid},{serial#}' IMMEDIATE;
```

**Historial:** un temporizador cada 15 s guarda una fila en `SNAPSHOT` y los valores en `VALOR_METRICA`, con `METRICA.nombre_oracle` = `metric_name`, y el gráfico lee de ahí. **Demo de bloqueo:** dos ventanas de SQL Developer hacen `UPDATE demo_ventas.cliente SET nombre = nombre WHERE id_cliente = 1` sin COMMIT. La app muestra el bloqueo y lo resuelve matando la sesión que bloquea.

## 5. Auditoría (unificada)

Vistas: `UNIFIED_AUDIT_TRAIL`, `AUDIT_UNIFIED_POLICIES`, `AUDIT_UNIFIED_ENABLED_POLICIES`.

```sql
-- Políticas y si están activas
SELECT p.policy_name,
       MAX(CASE WHEN e.policy_name IS NOT NULL THEN 'SI' ELSE 'NO' END) activa,
       COUNT(*) acciones
FROM audit_unified_policies p
LEFT JOIN audit_unified_enabled_policies e ON e.policy_name = p.policy_name
GROUP BY p.policy_name ORDER BY 1;

CREATE AUDIT POLICY POL_DEMO_DML ACTIONS INSERT, UPDATE, DELETE ON DEMO_VENTAS.FACTURA;
CREATE AUDIT POLICY POL_DDL ACTIONS CREATE TABLE, DROP TABLE, ALTER TABLE;
AUDIT POLICY POL_DEMO_DML;                       -- o: BY {USUARIO} · WHENEVER NOT SUCCESSFUL
NOAUDIT POLICY POL_DEMO_DML;
DROP AUDIT POLICY POL_DEMO_DML;                  -- primero NOAUDIT
AUDIT POLICY ORA_LOGON_FAILURES;                 -- predefinida: logins fallidos

-- Pista con filtros (todo con bind)
SELECT event_timestamp, dbusername, action_name, object_schema, object_name,
       return_code, unified_audit_policies, sql_text
FROM unified_audit_trail
WHERE event_timestamp BETWEEN :desde AND :hasta
  AND (:usuario IS NULL OR dbusername = :usuario)
  AND (:accion  IS NULL OR action_name = :accion)
ORDER BY event_timestamp DESC FETCH FIRST 500 ROWS ONLY;

-- Depurar la pista
BEGIN DBMS_AUDIT_MGMT.CLEAN_AUDIT_TRAIL(DBMS_AUDIT_MGMT.AUDIT_TRAIL_UNIFIED, use_last_arch_timestamp => FALSE); END;
```

Convención: las políticas creadas desde la app empiezan con `POL_`, y `99_limpieza.sql` las borra.

## 6. Seguridad de usuarios

Vistas: `DBA_USERS`, `DBA_ROLES`, `DBA_ROLE_PRIVS`, `DBA_SYS_PRIVS`, `DBA_TAB_PRIVS`, `DBA_PROFILES`, `DBA_TS_QUOTAS`.

```sql
SELECT username, account_status, lock_date, expiry_date, default_tablespace, profile, created
FROM dba_users WHERE oracle_maintained = 'N' ORDER BY username;

CREATE USER {USR} IDENTIFIED BY "{clave}" DEFAULT TABLESPACE {TS} QUOTA 50M ON {TS} PROFILE {PERFIL};
ALTER USER {USR} ACCOUNT LOCK;            -- ACCOUNT UNLOCK
ALTER USER {USR} PASSWORD EXPIRE;
ALTER USER {USR} IDENTIFIED BY "{clave}";
DROP USER {USR} CASCADE;

SELECT role, oracle_maintained FROM dba_roles ORDER BY oracle_maintained, role;
CREATE ROLE {ROL};
GRANT CREATE SESSION TO {ROL};
GRANT SELECT ON DEMO_VENTAS.FACTURA TO {ROL};
GRANT {ROL} TO {USR};
REVOKE {ROL} FROM {USR};

-- Privilegios de un usuario (directos y por rol)
SELECT 'ROL' tipo, granted_role privilegio, NULL objeto FROM dba_role_privs WHERE grantee = :u
UNION ALL SELECT 'SISTEMA', privilege, NULL FROM dba_sys_privs WHERE grantee = :u
UNION ALL SELECT 'OBJETO', privilege, owner || '.' || table_name FROM dba_tab_privs WHERE grantee = :u;

CREATE PROFILE {PERFIL} LIMIT
  FAILED_LOGIN_ATTEMPTS 3  PASSWORD_LOCK_TIME 1/1440  PASSWORD_LIFE_TIME 90
  SESSIONS_PER_USER 2      IDLE_TIME 15;
SELECT profile, resource_name, limit FROM dba_profiles WHERE profile = :p ORDER BY resource_type, resource_name;
```

> En la propuesta original el perfil traía `PASSWORD_VERIFY_FUNCTION ora12c_verify_function`. Se quitó porque falla si no se corrió `utlpwdmg.sql` en la PDB. `PASSWORD_LOCK_TIME 1/1440` bloquea por **1 minuto**, lo justo para la demo.

**Demo:** crear el usuario `JUAN` con el perfil, fallar el login 3 veces y mostrar `LOCKED(TIMED)`, y luego desbloquearlo. Se conecta con Auditoría (`ORA_LOGON_FAILURES`).

## 7. Respaldos (Data Pump con `DBMS_DATAPUMP`)

```sql
DECLARE
  h      NUMBER;
  estado VARCHAR2(30);
BEGIN
  -- job_mode: 'FULL' | 'SCHEMA' | 'TABLE'
  h := DBMS_DATAPUMP.OPEN(operation => 'EXPORT', job_mode => 'SCHEMA', job_name => 'EXP_SCHEMA_20261027_153000');
  DBMS_DATAPUMP.ADD_FILE(h, 'EXP_SCHEMA_20261027_153000.DMP', 'DIR_RESPALDOS');
  DBMS_DATAPUMP.ADD_FILE(h, 'EXP_SCHEMA_20261027_153000.LOG', 'DIR_RESPALDOS',
                         filetype => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);
  DBMS_DATAPUMP.METADATA_FILTER(h, 'SCHEMA_EXPR', 'IN (''DEMO_VENTAS'')');
  -- Solo modo TABLE: además filtrar las tablas
  -- DBMS_DATAPUMP.METADATA_FILTER(h, 'NAME_EXPR', 'IN (''FACTURA'',''CLIENTE'')');
  -- Solo modo FULL: sin filtros
  DBMS_DATAPUMP.START_JOB(h);
  DBMS_DATAPUMP.WAIT_FOR_JOB(h, estado);         -- 'COMPLETED' si salió bien
  :estado := estado;
END;
```

✅ Probado en XE 21.3: el export del esquema `DEMO_VENTAS` genera un `.dmp` de unos 6 MB en pocos segundos.

```sql
-- Progreso en vivo (desde OTRA conexión mientras corre)
SELECT job_name, operation, job_mode, state FROM dba_datapump_jobs WHERE owner_name = 'ADMIN_BD';
SELECT opname, sofar, totalwork, ROUND(sofar/NULLIF(totalwork,0)*100) pct, time_remaining
FROM v$session_longops WHERE opname LIKE 'EXP_%' AND sofar <> totalwork;

-- Tamaño del archivo resultante
DECLARE ex BOOLEAN; len NUMBER; bs NUMBER;
BEGIN UTL_FILE.FGETATTR('DIR_RESPALDOS', :archivo, ex, len, bs); :tamano := len; END;
```

**Flujo en la app:**

1. Se inserta la fila en `RESPALDO` (`EN_PROCESO`) y sus filas en `RESPALDO_OBJETO`.
2. Se ejecuta el bloque en un `Task` sin congelar la pantalla.
3. Se actualizan `estado`, `fecha_fin` y `tamano_bytes`.
4. Se muestra el `.log`.
5. Se registra en la bitácora.

Equivalente en consola, para probar: `expdp admin_bd@localhost:1521/XEPDB1 schemas=DEMO_VENTAS directory=DIR_RESPALDOS dumpfile=demo.dmp logfile=demo.log`. Para tablas se usa `tables=DEMO_VENTAS.FACTURA` y para la base completa, `full=y`.

## 8. Recuperación

```sql
DECLARE
  h      NUMBER;
  estado VARCHAR2(30);
BEGIN
  h := DBMS_DATAPUMP.OPEN(operation => 'IMPORT', job_mode => 'TABLE', job_name => 'IMP_TABLE_20261027_160000');
  DBMS_DATAPUMP.ADD_FILE(h, 'EXP_SCHEMA_20261027_153000.DMP', 'DIR_RESPALDOS');
  DBMS_DATAPUMP.ADD_FILE(h, 'IMP_TABLE_20261027_160000.LOG', 'DIR_RESPALDOS',
                         filetype => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);
  DBMS_DATAPUMP.METADATA_FILTER(h, 'SCHEMA_EXPR', 'IN (''DEMO_VENTAS'')');
  DBMS_DATAPUMP.METADATA_FILTER(h, 'NAME_EXPR',   'IN (''DETALLE_FACTURA'')');
  DBMS_DATAPUMP.SET_PARAMETER(h, 'TABLE_EXISTS_ACTION', 'REPLACE');   -- SKIP | APPEND | TRUNCATE | REPLACE
  -- Restaurar en otro esquema:
  -- DBMS_DATAPUMP.METADATA_REMAP(h, 'REMAP_SCHEMA', 'DEMO_VENTAS', 'DEMO_COPIA');
  DBMS_DATAPUMP.START_JOB(h);
  DBMS_DATAPUMP.WAIT_FOR_JOB(h, estado);
  :estado := estado;
END;
```

✅ Probado en XE 21.3: `DROP TABLE demo_ventas.detalle_factura PURGE` → restaurarla desde el respaldo del esquema → `SELECT COUNT(*)` devuelve **200 000**.

El modo del import no tiene que coincidir con el del export: se puede sacar **una tabla** de un respaldo de esquema o full. Para restaurar el esquema completo como `DEMO_COPIA` se usa `job_mode => 'SCHEMA'` con `REMAP_SCHEMA`, y Data Pump crea el usuario.

## 9. Directorios y archivos

```sql
SELECT directory_name, directory_path FROM dba_directories
WHERE origin_con_id = SYS_CONTEXT('USERENV','CON_ID') OR directory_name LIKE 'DIR\_%' ESCAPE '\'
ORDER BY 1;

CREATE OR REPLACE DIRECTORY {DIR} AS 'D:\respaldos';
GRANT READ, WRITE ON DIRECTORY {DIR} TO {USR};
DROP DIRECTORY {DIR};

-- Borrar / renombrar un archivo desde Oracle
BEGIN UTL_FILE.FREMOVE('DIR_RESPALDOS', :archivo); END;
BEGIN UTL_FILE.FRENAME('DIR_RESPALDOS', :origen, 'DIR_RESPALDOS', :destino, FALSE); END;
```

**Listar archivos:** la app corre en la misma máquina que la BD, así que se usa `new DirectoryInfo(ruta).GetFiles("*.dmp")` (y `*.log`) para mostrar el nombre, el tamaño y la fecha. Para **ver un `.log`** se usa `File.ReadAllText`. Además se **cruza con `RESPALDO`**: se marcan los respaldos cuyo archivo ya no existe y los archivos que no están en el catálogo. Al borrar un archivo, el respaldo pasa a `ELIMINADO`. La comparación de nombres no distingue mayúsculas.

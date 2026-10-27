-- =====================================================================
--  OraAdmin XE · 01_modelo_3fn.sql
--  Modelo de datos propio de la app (3FN): catálogo de respaldos y
--  restauraciones, programaciones, bitácora e historial de rendimiento.
--  Ver docs/modelo-datos.md para el diagrama y la justificación de 3FN.
--
--  Ejecutar conectado como ADMIN_BD:
--     sqlplus admin_bd@localhost:1521/XEPDB1 @sql/01_modelo_3fn.sql
-- =====================================================================
SET FEEDBACK ON
WHENEVER SQLERROR EXIT SQL.SQLCODE

PROMPT >> Catálogos
CREATE TABLE tipo_respaldo (
  id_tipo      NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_tipo_respaldo PRIMARY KEY,
  codigo       VARCHAR2(10)  NOT NULL CONSTRAINT uq_tipo_codigo UNIQUE
               CONSTRAINT ck_tipo_codigo CHECK (codigo IN ('FULL','SCHEMA','TABLE')),
  descripcion  VARCHAR2(200) NOT NULL
);

CREATE TABLE directorio (
  id_directorio  NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_directorio PRIMARY KEY,
  nombre_oracle  VARCHAR2(128) NOT NULL CONSTRAINT uq_dir_nombre UNIQUE,
  ruta_so        VARCHAR2(400) NOT NULL,
  fecha_registro DATE DEFAULT SYSDATE NOT NULL,
  activo         CHAR(1) DEFAULT 'S' NOT NULL CONSTRAINT ck_dir_activo CHECK (activo IN ('S','N'))
);

CREATE TABLE modulo (
  id_modulo NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_modulo PRIMARY KEY,
  nombre    VARCHAR2(50) NOT NULL CONSTRAINT uq_modulo_nombre UNIQUE
);

CREATE TABLE metrica (
  id_metrica    NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_metrica PRIMARY KEY,
  nombre        VARCHAR2(64)  NOT NULL CONSTRAINT uq_metrica_nombre UNIQUE,
  nombre_oracle VARCHAR2(64)  NOT NULL CONSTRAINT uq_metrica_oracle UNIQUE,  -- METRIC_NAME en V$SYSMETRIC
  unidad        VARCHAR2(20)  NOT NULL
);

PROMPT >> Respaldos y restauraciones
CREATE TABLE respaldo (
  id_respaldo   NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_respaldo PRIMARY KEY,
  id_tipo       NUMBER NOT NULL CONSTRAINT fk_resp_tipo REFERENCES tipo_respaldo(id_tipo),
  id_directorio NUMBER NOT NULL CONSTRAINT fk_resp_dir  REFERENCES directorio(id_directorio),
  nombre_job    VARCHAR2(128) NOT NULL CONSTRAINT uq_resp_job UNIQUE,
  archivo_dump  VARCHAR2(200) NOT NULL,
  archivo_log   VARCHAR2(200) NOT NULL,
  fecha_inicio  DATE DEFAULT SYSDATE NOT NULL,
  fecha_fin     DATE,
  estado        VARCHAR2(12) DEFAULT 'EN_PROCESO' NOT NULL
                CONSTRAINT ck_resp_estado CHECK (estado IN ('EN_PROCESO','COMPLETADO','ERROR','ELIMINADO')),
  tamano_bytes  NUMBER,
  usuario_bd    VARCHAR2(128) NOT NULL,
  observacion   VARCHAR2(500),
  CONSTRAINT uq_resp_archivo UNIQUE (id_directorio, archivo_dump),
  CONSTRAINT ck_resp_fechas  CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio)
);

CREATE TABLE respaldo_objeto (
  id_objeto   NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_resp_obj PRIMARY KEY,
  id_respaldo NUMBER NOT NULL CONSTRAINT fk_respobj_resp REFERENCES respaldo(id_respaldo) ON DELETE CASCADE,
  esquema     VARCHAR2(128) NOT NULL,
  tabla       VARCHAR2(128),            -- NULL cuando se respalda el esquema completo
  CONSTRAINT uq_resp_obj UNIQUE (id_respaldo, esquema, tabla)
);

CREATE TABLE restauracion (
  id_restauracion  NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_restauracion PRIMARY KEY,
  id_respaldo      NUMBER NOT NULL CONSTRAINT fk_rest_resp REFERENCES respaldo(id_respaldo),
  nombre_job       VARCHAR2(128) NOT NULL CONSTRAINT uq_rest_job UNIQUE,
  esquema_destino  VARCHAR2(128),       -- NULL = mismo esquema (sin REMAP_SCHEMA)
  accion_existente VARCHAR2(10) DEFAULT 'SKIP' NOT NULL
                   CONSTRAINT ck_rest_accion CHECK (accion_existente IN ('SKIP','APPEND','TRUNCATE','REPLACE')),
  archivo_log      VARCHAR2(200) NOT NULL,  -- se escribe en el mismo directorio del respaldo
  fecha_inicio     DATE DEFAULT SYSDATE NOT NULL,
  fecha_fin        DATE,
  estado           VARCHAR2(12) DEFAULT 'EN_PROCESO' NOT NULL
                   CONSTRAINT ck_rest_estado CHECK (estado IN ('EN_PROCESO','COMPLETADO','ERROR')),
  usuario_bd       VARCHAR2(128) NOT NULL,
  observacion      VARCHAR2(500),
  CONSTRAINT ck_rest_fechas CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio)
);

CREATE TABLE restauracion_objeto (
  id_objeto       NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_rest_obj PRIMARY KEY,
  id_restauracion NUMBER NOT NULL CONSTRAINT fk_restobj_rest REFERENCES restauracion(id_restauracion) ON DELETE CASCADE,
  esquema         VARCHAR2(128) NOT NULL,
  tabla           VARCHAR2(128),
  CONSTRAINT uq_rest_obj UNIQUE (id_restauracion, esquema, tabla)
);

PROMPT >> Respaldos programados (extra)
CREATE TABLE programacion (
  id_programacion NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_programacion PRIMARY KEY,
  id_tipo         NUMBER NOT NULL CONSTRAINT fk_prog_tipo REFERENCES tipo_respaldo(id_tipo),
  id_directorio   NUMBER NOT NULL CONSTRAINT fk_prog_dir  REFERENCES directorio(id_directorio),
  nombre_job      VARCHAR2(128) NOT NULL CONSTRAINT uq_prog_job UNIQUE,   -- nombre en DBMS_SCHEDULER
  frecuencia      VARCHAR2(200) NOT NULL,                                 -- ej. 'FREQ=DAILY;BYHOUR=22'
  activo          CHAR(1) DEFAULT 'S' NOT NULL CONSTRAINT ck_prog_activo CHECK (activo IN ('S','N'))
);

CREATE TABLE programacion_objeto (
  id_objeto       NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_prog_obj PRIMARY KEY,
  id_programacion NUMBER NOT NULL CONSTRAINT fk_progobj_prog REFERENCES programacion(id_programacion) ON DELETE CASCADE,
  esquema         VARCHAR2(128) NOT NULL,
  tabla           VARCHAR2(128),
  CONSTRAINT uq_prog_obj UNIQUE (id_programacion, esquema, tabla)
);

PROMPT >> Bitácora
CREATE TABLE bitacora (
  id_bitacora NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_bitacora PRIMARY KEY,
  id_modulo   NUMBER NOT NULL CONSTRAINT fk_bit_modulo REFERENCES modulo(id_modulo),
  fecha       TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
  usuario_bd  VARCHAR2(128) NOT NULL,
  accion      VARCHAR2(100) NOT NULL,
  sentencia   CLOB,
  resultado   VARCHAR2(5) NOT NULL CONSTRAINT ck_bit_resultado CHECK (resultado IN ('OK','ERROR')),
  mensaje     VARCHAR2(4000)
);

PROMPT >> Historial de rendimiento
CREATE TABLE snapshot (
  id_snapshot NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_snapshot PRIMARY KEY,
  fecha       TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
);

CREATE TABLE valor_metrica (
  id_snapshot NUMBER NOT NULL CONSTRAINT fk_val_snap    REFERENCES snapshot(id_snapshot) ON DELETE CASCADE,
  id_metrica  NUMBER NOT NULL CONSTRAINT fk_val_metrica REFERENCES metrica(id_metrica),
  valor       NUMBER NOT NULL,
  CONSTRAINT pk_valor_metrica PRIMARY KEY (id_snapshot, id_metrica)
);

PROMPT >> Índices en llaves foráneas (Oracle no los crea solo)
CREATE INDEX ix_resp_tipo    ON respaldo(id_tipo);
CREATE INDEX ix_resp_dir     ON respaldo(id_directorio);
CREATE INDEX ix_rest_resp    ON restauracion(id_respaldo);
CREATE INDEX ix_prog_tipo    ON programacion(id_tipo);
CREATE INDEX ix_prog_dir     ON programacion(id_directorio);
CREATE INDEX ix_bit_modulo   ON bitacora(id_modulo);
CREATE INDEX ix_bit_fecha    ON bitacora(fecha);
CREATE INDEX ix_snap_fecha   ON snapshot(fecha);
CREATE INDEX ix_val_metrica  ON valor_metrica(id_metrica);

PROMPT >> Vistas (los datos derivados se calculan, no se guardan)
CREATE OR REPLACE VIEW v_respaldos AS
SELECT r.id_respaldo, t.codigo AS tipo, d.nombre_oracle AS directorio, d.ruta_so,
       r.nombre_job, r.archivo_dump, r.archivo_log, r.estado, r.fecha_inicio, r.fecha_fin,
       ROUND((r.fecha_fin - r.fecha_inicio) * 86400) AS duracion_seg,
       ROUND(r.tamano_bytes / 1024 / 1024, 2)        AS tamano_mb,
       r.usuario_bd, r.observacion
FROM respaldo r
JOIN tipo_respaldo t ON t.id_tipo = r.id_tipo
JOIN directorio d    ON d.id_directorio = r.id_directorio;

CREATE OR REPLACE VIEW v_restauraciones AS
SELECT x.id_restauracion, x.id_respaldo, r.archivo_dump, d.nombre_oracle AS directorio,
       x.nombre_job, x.esquema_destino, x.accion_existente, x.archivo_log, x.estado,
       x.fecha_inicio, x.fecha_fin,
       ROUND((x.fecha_fin - x.fecha_inicio) * 86400) AS duracion_seg,
       x.usuario_bd, x.observacion
FROM restauracion x
JOIN respaldo r   ON r.id_respaldo = x.id_respaldo
JOIN directorio d ON d.id_directorio = r.id_directorio;

CREATE OR REPLACE VIEW v_bitacora AS
SELECT b.id_bitacora, b.fecha, m.nombre AS modulo, b.usuario_bd, b.accion,
       b.resultado, b.mensaje, b.sentencia
FROM bitacora b
JOIN modulo m ON m.id_modulo = b.id_modulo;

PROMPT >> Datos de catálogo
INSERT INTO tipo_respaldo (codigo, descripcion) VALUES ('FULL',   'Base de datos completa (PDB)');
INSERT INTO tipo_respaldo (codigo, descripcion) VALUES ('SCHEMA', 'Uno o varios esquemas');
INSERT INTO tipo_respaldo (codigo, descripcion) VALUES ('TABLE',  'Una o varias tablas');

INSERT INTO modulo (nombre)
  SELECT column_value FROM TABLE(sys.odcivarchar2list(
    'GENERAL','TABLESPACES','TUNING','PERFORMANCE','AUDITORIA','SEGURIDAD',
    'RESPALDOS','RECUPERACION','DIRECTORIOS'));

-- Nombres de V$CON_SYSMETRIC (métricas de la PDB; V$SYSMETRIC viene vacía dentro de XEPDB1)
INSERT INTO metrica (nombre, nombre_oracle, unidad) VALUES ('SESIONES_ACTIVAS',  'Average Active Sessions',  'sesiones');
INSERT INTO metrica (nombre, nombre_oracle, unidad) VALUES ('CPU_POR_SEG',       'CPU Usage Per Sec',        'cs/s');
INSERT INTO metrica (nombre, nombre_oracle, unidad) VALUES ('LECTURAS_LOGICAS',  'Logical Reads Per Sec',    'lect/s');
INSERT INTO metrica (nombre, nombre_oracle, unidad) VALUES ('EJECUCIONES_SEG',   'Executions Per Sec',       'ejec/s');
INSERT INTO metrica (nombre, nombre_oracle, unidad) VALUES ('TRANSACCIONES_SEG', 'User Transaction Per Sec', 'tx/s');

-- Registra el directorio creado por 00_setup_sys.sql
INSERT INTO directorio (nombre_oracle, ruta_so)
  SELECT directory_name, directory_path FROM all_directories WHERE directory_name = 'DIR_RESPALDOS';

COMMIT;
PROMPT >> Modelo creado. Siguiente paso: sqlplus admin_bd@localhost:1521/XEPDB1 @sql/02_demo_ventas.sql
EXIT

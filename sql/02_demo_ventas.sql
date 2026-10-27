-- =====================================================================
--  OraAdmin XE · 02_demo_ventas.sql
--  Esquema de prueba DEMO_VENTAS (3FN) con datos generados para tener
--  algo que respaldar, borrar, restaurar, auditar y optimizar.
--
--  Ejecutar conectado como ADMIN_BD (tiene rol DBA):
--     sqlplus admin_bd@localhost:1521/XEPDB1 @sql/02_demo_ventas.sql
--
--  Volumen: 2 000 clientes, 500 productos, 50 000 facturas y
--  200 000 detalles (~20 MB). A propósito NO se indexa
--  DETALLE_FACTURA(ID_PRODUCTO): es la demo del módulo de Tuning.
-- =====================================================================
SET VERIFY OFF
SET FEEDBACK ON
WHENEVER SQLERROR EXIT SQL.SQLCODE

ACCEPT clave_demo CHAR PROMPT 'Contraseña para DEMO_VENTAS: ' HIDE

PROMPT >> Usuario DEMO_VENTAS
CREATE USER demo_ventas IDENTIFIED BY "&clave_demo"
  DEFAULT TABLESPACE users
  TEMPORARY TABLESPACE temp
  QUOTA UNLIMITED ON users;
GRANT CREATE SESSION, CREATE TABLE, CREATE VIEW, CREATE SEQUENCE TO demo_ventas;

PROMPT >> Tablas
CREATE TABLE demo_ventas.cliente (
  id_cliente     NUMBER        CONSTRAINT pk_cliente PRIMARY KEY,
  cedula         VARCHAR2(20)  NOT NULL CONSTRAINT uq_cliente_cedula UNIQUE,
  nombre         VARCHAR2(100) NOT NULL,
  correo         VARCHAR2(120),
  provincia      VARCHAR2(30)  NOT NULL,
  fecha_registro DATE DEFAULT SYSDATE NOT NULL
);

CREATE TABLE demo_ventas.producto (
  id_producto  NUMBER        CONSTRAINT pk_producto PRIMARY KEY,
  codigo       VARCHAR2(20)  NOT NULL CONSTRAINT uq_producto_codigo UNIQUE,
  nombre       VARCHAR2(100) NOT NULL,
  precio_lista NUMBER(12,2)  NOT NULL CONSTRAINT ck_producto_precio CHECK (precio_lista > 0)
);

CREATE TABLE demo_ventas.factura (
  id_factura NUMBER CONSTRAINT pk_factura PRIMARY KEY,
  id_cliente NUMBER NOT NULL CONSTRAINT fk_factura_cliente REFERENCES demo_ventas.cliente(id_cliente),
  fecha      DATE   NOT NULL
);

CREATE TABLE demo_ventas.detalle_factura (
  id_factura      NUMBER       NOT NULL CONSTRAINT fk_det_factura  REFERENCES demo_ventas.factura(id_factura),
  id_producto     NUMBER       NOT NULL CONSTRAINT fk_det_producto REFERENCES demo_ventas.producto(id_producto),
  cantidad        NUMBER(5)    NOT NULL CONSTRAINT ck_det_cantidad CHECK (cantidad > 0),
  precio_unitario NUMBER(12,2) NOT NULL CONSTRAINT ck_det_precio   CHECK (precio_unitario > 0),
  CONSTRAINT pk_detalle_factura PRIMARY KEY (id_factura, id_producto)
);

CREATE INDEX demo_ventas.ix_factura_cliente ON demo_ventas.factura(id_cliente);

PROMPT >> Datos generados
INSERT INTO demo_ventas.cliente (id_cliente, cedula, nombre, correo, provincia, fecha_registro)
SELECT LEVEL,
       TO_CHAR(100000000 + LEVEL),
       'Cliente ' || LPAD(LEVEL, 4, '0'),
       'cliente' || LEVEL || '@correo.cr',
       CASE MOD(LEVEL, 7) WHEN 0 THEN 'San José' WHEN 1 THEN 'Alajuela' WHEN 2 THEN 'Cartago'
            WHEN 3 THEN 'Heredia' WHEN 4 THEN 'Guanacaste' WHEN 5 THEN 'Puntarenas' ELSE 'Limón' END,
       DATE '2024-01-01' + MOD(LEVEL * 37, 1000)
FROM dual CONNECT BY LEVEL <= 2000;

INSERT INTO demo_ventas.producto (id_producto, codigo, nombre, precio_lista)
SELECT LEVEL,
       'P-' || LPAD(LEVEL, 5, '0'),
       'Producto ' || LPAD(LEVEL, 3, '0'),
       ROUND(DBMS_RANDOM.VALUE(500, 50000), 2)
FROM dual CONNECT BY LEVEL <= 500;

INSERT INTO demo_ventas.factura (id_factura, id_cliente, fecha)
SELECT LEVEL,
       MOD(LEVEL * 7919, 2000) + 1,
       DATE '2025-01-01' + MOD(LEVEL, 600)
FROM dual CONNECT BY LEVEL <= 50000;

-- 4 productos distintos por factura (id_producto entre 1 y 499)
INSERT INTO demo_ventas.detalle_factura (id_factura, id_producto, cantidad, precio_unitario)
SELECT TRUNC((LEVEL - 1) / 4) + 1,
       MOD(LEVEL - 1, 4) + 1 + MOD(TRUNC((LEVEL - 1) / 4), 496),
       TRUNC(DBMS_RANDOM.VALUE(1, 10)),
       ROUND(DBMS_RANDOM.VALUE(500, 50000), 2)
FROM dual CONNECT BY LEVEL <= 200000;

COMMIT;

PROMPT >> Estadísticas iniciales
EXEC DBMS_STATS.GATHER_SCHEMA_STATS('DEMO_VENTAS');

SELECT 'cliente' tabla, COUNT(*) filas FROM demo_ventas.cliente UNION ALL
SELECT 'producto',        COUNT(*) FROM demo_ventas.producto UNION ALL
SELECT 'factura',         COUNT(*) FROM demo_ventas.factura UNION ALL
SELECT 'detalle_factura', COUNT(*) FROM demo_ventas.detalle_factura;

PROMPT >> Esquema de prueba listo.
EXIT

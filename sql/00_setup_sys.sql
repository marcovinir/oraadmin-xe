-- =====================================================================
--  OraAdmin XE · 00_setup_sys.sql
--  Crea el usuario dueño de la app (ADMIN_BD), sus privilegios y el
--  directorio de respaldos dentro de la PDB XEPDB1.
--
--  Ejecutar UNA vez como SYS (desde la carpeta del repo):
--     sqlplus / as sysdba @sql/00_setup_sys.sql
--  o  sqlplus sys@localhost:1521/XEPDB1 as sysdba @sql/00_setup_sys.sql
--
--  Antes: crear la carpeta de respaldos en el disco (Oracle no la crea):
--     mkdir C:\respaldos_bd
-- =====================================================================
SET VERIFY OFF
SET FEEDBACK ON
WHENEVER SQLERROR EXIT SQL.SQLCODE

ALTER SESSION SET CONTAINER = XEPDB1;

ACCEPT clave_admin    CHAR PROMPT 'Contraseña para ADMIN_BD: ' HIDE
ACCEPT ruta_respaldos CHAR DEFAULT 'C:\respaldos_bd' PROMPT 'Carpeta de respaldos [C:\respaldos_bd]: '

PROMPT >> 1. Usuario dueño de la app
CREATE USER admin_bd IDENTIFIED BY "&clave_admin"
  DEFAULT TABLESPACE users
  TEMPORARY TABLESPACE temp
  QUOTA UNLIMITED ON users;

PROMPT >> 2. Privilegios de DBA, auditoría y Data Pump
GRANT DBA TO admin_bd;
GRANT AUDIT_ADMIN, AUDIT_VIEWER TO admin_bd;
GRANT DATAPUMP_EXP_FULL_DATABASE, DATAPUMP_IMP_FULL_DATABASE TO admin_bd;
GRANT SELECT ANY DICTIONARY TO admin_bd;
-- Privilegios directos (no por rol) para poder usarlos dentro de PL/SQL
GRANT EXECUTE ON SYS.DBMS_DATAPUMP TO admin_bd;
GRANT EXECUTE ON SYS.UTL_FILE      TO admin_bd;
GRANT EXECUTE ON SYS.DBMS_LOCK     TO admin_bd;
GRANT CREATE ANY DIRECTORY, DROP ANY DIRECTORY TO admin_bd;
GRANT ALTER SYSTEM TO admin_bd;

PROMPT >> 3. Directorio de respaldos
CREATE OR REPLACE DIRECTORY dir_respaldos AS '&ruta_respaldos';
GRANT READ, WRITE ON DIRECTORY dir_respaldos TO admin_bd;

PROMPT >> Listo. Siguiente paso: sqlplus admin_bd@localhost:1521/XEPDB1 @sql/01_modelo_3fn.sql
EXIT

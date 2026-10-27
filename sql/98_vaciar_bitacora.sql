-- =====================================================================
--  OraAdmin XE · 98_vaciar_bitacora.sql
--  Deja la bitácora (y el historial de rendimiento) vacíos antes de la
--  presentación, para que solo se vea lo que se haga en vivo.
--  NO toca el catálogo de respaldos ni de restauraciones (el respaldo
--  previo del plan B se conserva).
--
--  Ejecutar conectado como ADMIN_BD:
--     sqlplus admin_bd@localhost:1521/XEPDB1 @sql/98_vaciar_bitacora.sql
-- =====================================================================
SET FEEDBACK ON
WHENEVER SQLERROR EXIT SQL.SQLCODE

PROMPT >> Bitácora
DELETE FROM bitacora;

PROMPT >> Historial de rendimiento (las muestras se borran en cascada)
DELETE FROM snapshot;

COMMIT;
PROMPT >> Listo: la bitácora está vacía.
EXIT

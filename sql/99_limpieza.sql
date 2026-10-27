-- =====================================================================
--  OraAdmin XE · 99_limpieza.sql
--  Borra TODO lo que crean los scripts y la app para reinstalar desde
--  cero (plan B de la presentación). No borra los archivos .dmp/.log
--  del disco.
--
--  Ejecutar como SYS:
--     sqlplus / as sysdba @sql/99_limpieza.sql
--  Luego repetir 00, 01 y 02.
-- =====================================================================
SET FEEDBACK OFF
ALTER SESSION SET CONTAINER = XEPDB1;
-- Después de cambiar de contenedor, para que se vea la salida
SET SERVEROUTPUT ON

DECLARE
  PROCEDURE ejecutar(p_sql VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE p_sql;
    DBMS_OUTPUT.PUT_LINE('OK    ' || p_sql);
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('SALTO ' || p_sql || '  (' || SQLERRM || ')');
  END;
BEGIN
  -- Sesiones abiertas de los usuarios que se van a borrar
  FOR s IN (SELECT sid, serial# FROM v$session
            WHERE username IN ('ADMIN_BD','DEMO_VENTAS','DEMO_COPIA')) LOOP
    ejecutar('ALTER SYSTEM KILL SESSION ''' || s.sid || ',' || s.serial# || ''' IMMEDIATE');
  END LOOP;

  -- Trabajos de Data Pump que hayan quedado colgados
  FOR j IN (SELECT owner_name, job_name FROM dba_datapump_jobs
            WHERE owner_name = 'ADMIN_BD' AND state = 'NOT RUNNING') LOOP
    ejecutar('DROP TABLE "' || j.owner_name || '"."' || j.job_name || '" PURGE');
  END LOOP;

  -- Políticas de auditoría creadas desde la app (prefijo POL_)
  FOR p IN (SELECT DISTINCT policy_name FROM audit_unified_enabled_policies
            WHERE policy_name LIKE 'POL\_%' ESCAPE '\') LOOP
    ejecutar('NOAUDIT POLICY ' || p.policy_name);
  END LOOP;
  FOR p IN (SELECT DISTINCT policy_name FROM audit_unified_policies
            WHERE policy_name LIKE 'POL\_%' ESCAPE '\') LOOP
    ejecutar('DROP AUDIT POLICY ' || p.policy_name);
  END LOOP;

  ejecutar('DROP USER demo_copia CASCADE');
  ejecutar('DROP USER demo_ventas CASCADE');
  ejecutar('DROP USER admin_bd CASCADE');
  ejecutar('DROP DIRECTORY dir_respaldos');
END;
/
PROMPT >> Limpieza terminada. Los tablespaces, usuarios, roles y perfiles creados en la demo
PROMPT >> se borran desde la app o a mano (ver docs/guion-demo.md).
EXIT

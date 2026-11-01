# Requisitos, instalación y primer arranque

**Requisitos:** Windows 10 u 11 y Oracle Database 21c XE con los servicios `OracleServiceXE` y `OracleOraDB21Home1TNSListener` iniciados. El ejecutable publicado no necesita instalar .NET ni Oracle Client.

## Preparar la base de datos (una sola vez)

1. Cree la carpeta de respaldos: `mkdir C:\respaldos_bd`.
2. Abra **cmd** en la carpeta del proyecto y ejecute los scripts en este orden:

   ```bat
   set NLS_LANG=SPANISH_COSTA RICA.AL32UTF8
   sqlplus / as sysdba @sql/00_setup_sys.sql
   sqlplus admin_bd@localhost:1521/XEPDB1 @sql/01_modelo_3fn.sql
   sqlplus admin_bd@localhost:1521/XEPDB1 @sql/02_demo_ventas.sql
   ```

3. Cuando `00_setup_sys.sql` lo pida, defina la contraseña de `ADMIN_BD`.

Si `/ as sysdba` responde `ORA-01031`, use `sqlplus sys@localhost:1521/XEPDB1 as sysdba`.

## Conectarse

1. Abra `OraAdminXE.exe`.
2. Escriba el servidor `localhost`, el puerto `1521`, el servicio `XEPDB1` y el usuario `ADMIN_BD` con su contraseña.
3. Presione **Conectar**. Solo entran los usuarios con rol DBA; la app recuerda la conexión, pero nunca la contraseña.

**Figura.** Pantalla de inicio de sesión.

**Captura:** `docs/capturas/01_login.png`

# Errores comunes

| Mensaje | Causa | Solución |
|---|---|---|
| `ORA-01017` | Usuario o contraseña incorrectos | Revisar las credenciales de `ADMIN_BD` |
| `ORA-12541` | El listener no está corriendo | Iniciar el servicio `OracleOraDB21Home1TNSListener` |
| `ORA-12514` | El servicio no existe | Usar `XEPDB1` como servicio, no `XE` |
| `ORA-39070` / `ORA-29283` | La carpeta del directorio no existe o no tiene permisos | Crear la carpeta y revisar los permisos del servicio de Oracle |
| `ORA-31634` | Ya existe un job de Data Pump con ese nombre | Reintentar: la app genera un nombre nuevo con fecha y hora |
| `ORA-01031` | Privilegios insuficientes | Conectarse con un usuario que tenga el rol DBA |
| `ORA-00942` | La tabla no existe, por ejemplo después de **Simular desastre** | Restaurarla en **Recuperación** |
| `ORA-02379` | Ya existe un perfil con ese nombre; `99_limpieza.sql` no borra los perfiles | Usar otro nombre o eliminarlo en **Seguridad → Perfiles** |

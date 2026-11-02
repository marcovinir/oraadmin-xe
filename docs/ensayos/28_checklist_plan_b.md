# Issue #28 · Checklist del ensayo y Plan B

## Antes del ensayo 1 (sábado 31 de octubre)

- [ ] Confirmar que se fusionaron las funcionalidades necesarias en la rama de entrega; usar una versión coherente del ejecutable.
- [ ] Designar quién maneja el teclado en cada paso; una persona cronometra y anota incidencias.
- [ ] Verificar **Oracle 21c XE/XEPDB1**, ADMIN_BD, DEMO_VENTAS y carpeta `C:\respaldos_bd` con permisos del servicio Oracle.
- [ ] Confirmar **respaldo completado y recuperable** del esquema DEMO_VENTAS, con DMP y LOG presentes y sin estado ELIMINADO.
- [ ] Preparar dos ventanas de SQL Developer conectadas a DEMO_VENTAS, sin dejar una transacción bloqueadora abierta al inicio.
- [ ] Preparar datos/objetos de prueba: TS_VENTAS, PERFIL_DEMO, JUAN, ROL_CONSULTA, POL_DEMO_DML, DIR_RESPALDOS2 y el índice de tuning **según el estado inicial acordado**. Evitar duplicados.
- [ ] Capturas de los diez módulos en `docs/capturas/`; **solo seis capturas de Persona 3 constan publicadas hasta ahora**.
- [ ] Comprobar presentación, resolución de pantalla y legibilidad de los diálogos.

## Ensayo 1 — sábado 31 de octubre

- [ ] Ejecutar **los diez pasos** de `28_guion_cronometrado.md` con cronómetro.
- [ ] Registrar duración real por paso, resultado y desviaciones en `28_registro_ensayos.md` y en la issue #28.
- [ ] Abrir issues `tipo: bug` únicamente para incidencias confirmadas; resolver bloqueantes sin agregar nuevas funcionalidades.
- [ ] Confirmar que la demo se puede repetir con limpieza segura y recreación de datos de prueba.

## Ensayo 2 — domingo 1 de noviembre

- [ ] Ejecutar en **entorno desechable y autorizado**, nunca sobre una BD importante, el reinicio con `99_limpieza.sql` y scripts `00_setup_sys.sql`, `01_modelo_3fn.sql`, `02_demo_ventas.sql`, después de verificar los efectos destructivos.
- [ ] Utilizar el **.exe del release real**, no el paquete provisional de una rama de desarrollo.
- [ ] Probar login, recorrido y las operaciones del guion; confirmar que el segundo ensayo se completa sin errores y en **≤ 15:00**.
- [ ] Registrar tiempos/resultado de cada paso en la issue #28.

## Plan B preparado (no implica todavía disponer físicamente de todos los recursos)

| Incidencia | Respuesta |
|---|---|
| .exe no abre | Ejecutable autocontenido previamente probado, copia USB y PC de respaldo del equipo |
| Oracle inaccesible | Verificar listener/servicio XEPDB1 y conectividad, no mostrar credenciales en pantalla |
| Respaldo full demasiado lento | Mostrar progreso/evidencia y usar DMP ya completado para Recuperación |
| Error de DIRECTORY / ORA-39070 / ORA-29283 | Verificar existencia y permisos de la carpeta, no borrar ni cambiar rutas sin revisar |
| SQL/índice/objeto ya existe | Usar estado inicial preparado y evitar repetir comandos destructivos |
| Restauración de tablas falla | Utilizar respaldo validado y capturas verificadas; no inventar recuperación exitosa |
| Bloqueo entre sesiones no se reproduce | Explicar con captura y métricas de ensayo; cerrar o revertir transacciones de prueba de forma controlada |
| Se pierde tiempo | Saltar al siguiente paso usando evidencia real preparada, avisando que se trata de captura |
| SmartScreen bloquea .exe | Validar procedencia, hash y firma si existe; no desactivar Defender globalmente |

## Kit físico de laboratorio

- [ ] USB con `OraAdminXE.exe` y **todos** los archivos que acompañan la publicación.
- [ ] Carpeta `sql/` (00, 01, 02 y 99) y manual de usuario PDF final.
- [ ] Carpeta con capturas de todos los módulos y opcionalmente video de demostración.
- [ ] Backup validado DMP/LOG en el host de Oracle o procedimiento para restaurarlo a entorno de ensayo.
- [ ] Plan de conexión alternativo, sin guardar contraseñas en texto claro.

**Nota de seguridad:** `DROP TABLE ... PURGE`, matar sesiones, eliminar DIRECTORY y `99_limpieza.sql` son acciones potencialmente destructivas. Ejecutarlas solo en una instancia de demostración, con respaldo probado y aprobación del equipo.

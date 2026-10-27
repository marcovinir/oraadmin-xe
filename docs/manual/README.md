# Manual de usuario (10 % de la nota)

**Máximo 4 páginas.** El archivo final va en esta carpeta como `manual_usuario.docx` y su versión `manual_usuario.pdf`. Las capturas van en `docs/capturas/`.

## Estructura acordada

| Página | Contenido | Responsable |
|:-:|---|---|
| 1 | Requisitos, instalación (scripts SQL en orden), primer arranque y login | Persona 1 |
| 2 | Inicio, Tablespaces, Seguridad y Auditoría: una captura y 3 o 4 pasos por módulo | Personas 1 y 3 |
| 3 | Tuning, Performance, Directorios y archivos | Personas 3 y 4 |
| 4 | Respaldos y Recuperación paso a paso, errores comunes y cómo resolverlos | Persona 2 |

**Coordina:** Persona 3, que junta las secciones, da el formato y exporta a PDF.

## Reglas de estilo

- Pasos cortos en imperativo: «Seleccione el tipo de respaldo», «Presione **Ejecutar**».
- Capturas recortadas a la parte relevante y numeradas (`Figura 3. Asistente de respaldo`).
- **No explicar teoría de Oracle.** El manual es para quien usa la app, no un resumen del curso.
- Misma fuente y mismos colores que la app (Segoe UI, acento rojo `#C74634`).

## Errores comunes (para la página 4)

| Mensaje | Causa | Solución |
|---|---|---|
| `ORA-01017` | Usuario o contraseña incorrectos | Revisar las credenciales de `ADMIN_BD` |
| `ORA-12541` | El listener no está corriendo | Iniciar el servicio `OracleOraDB21Home1TNSListener` |
| `ORA-12514` | El servicio no existe | Usar `XEPDB1` como servicio, no `XE` |
| `ORA-39070` / `ORA-29283` | La carpeta del directorio no existe o no tiene permisos | Crear la carpeta y revisar los permisos del servicio de Oracle |
| `ORA-31634` | Ya existe un job de Data Pump con ese nombre | Reintentar: la app genera un nombre nuevo con fecha y hora |
| `ORA-01031` | Privilegios insuficientes | Conectarse con un usuario que tenga el rol DBA |

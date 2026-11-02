# Issue #28 · Guion cronometrado de demostración (propuesta para ensayos)

**Entrega:** lunes 2 de noviembre de 2026. **Objetivo:** presentación de hasta 15 minutos.  
**Fuente:** `docs/guion-demo.md`; se mantienen los diez pasos y sus responsables.  
**Advertencia:** el guion original suma **16 minutos**, no 15. Este presupuesto suma **14 min 15 s** y reserva **45 s** para transiciones o imprevistos. Son **tiempos objetivo, no resultados medidos**.

| Paso | Responsable | Módulo | Demostración mínima | Objetivo | Tiempo acumulado |
|---:|---|---|---|---:|---:|
| 1 | Persona 1 | Login / Inicio | ADMIN_BD en XEPDB1; rechazo sin DBA; indicadores iniciales | 1:00 | 1:00 |
| 2 | Persona 3 | Tablespaces | TS_VENTAS 50 MB y autoextend, datafile y estados READ ONLY/WRITE; mostrar SQL | 1:30 | 2:30 |
| 3 | Personas 2 y 1 | Seguridad | PERFIL_DEMO, usuario JUAN, ROL_CONSULTA y privilegio SELECT | 1:30 | 4:00 |
| 4 | Persona 3 | Auditoría | ORA_LOGON_FAILURES, POL_DEMO_DML; fallos de login y pista | 1:30 | 5:30 |
| 5 | Persona 4 | Tuning | Plan original, índice/estadísticas y comparador del plan | 1:45 | 7:15 |
| 6 | Persona 4 | Performance | Métricas, bloqueo entre dos sesiones, identificar bloqueador y actuar con confirmación | 1:45 | 9:00 |
| 7 | Persona 2 | Respaldos | Tipos tabla/esquema/full; progreso, log y catálogo | 1:45 | 10:45 |
| 8 | Persona 2 | Recuperación | Restaurar DETALLE_FACTURA desde respaldo válido y mostrar conteo; remapeo si alcanza | 2:00 | 12:45 |
| 9 | Persona 3 | Directorios y archivos | DIRECTORY, DMP/LOG, visor y estado del catálogo | 1:00 | 13:45 |
| 10 | Persona 2 | Bitácora | Filtrar y mostrar SQL y resultados de acciones | 0:30 | 14:15 |
| — | Todos | Margen | Transiciones y latencias | 0:45 | **15:00** |

## Conducción y teclado

- **Persona 1**: controla login e Inicio; cede teclado a Persona 3.
- **Persona 3**: controla Tablespaces y Auditoría; después Directorios y archivos.
- **Persona 2**: perfiles de Seguridad, Respaldos, Recuperación y Bitácora.
- **Persona 4**: Tuning y Performance, con **dos sesiones SQL Developer** abiertas de antemano para demostrar bloqueos; solo ejecutar en DEMO_VENTAS aislado.
- En el paso 3 el teclado se transfiere de Persona 2 a Persona 1 según el guion; coordinarlo antes de iniciar.
- Una persona adicional cronometra y anuncia los puntos de tiempo por gesto, sin interrumpir la explicación.

## Guion verbal breve de Persona 3

**Tablespaces (90 s):** «Desde Tablespaces observamos uso y archivos físicos. Creo TS_VENTAS con tamaño inicial de 50 MB y crecimiento automático; reviso el SQL antes de confirmar. Muestro el datafile, sus ajustes y el cambio READ ONLY / READ WRITE, protegiendo los espacios de sistema».

**Auditoría (90 s):** «En Políticas activamos la auditoría de intentos fallidos y configuramos POL_DEMO_DML para FACTURA. Verificamos el estado; desde Pista de auditoría filtramos los eventos y consultamos su detalle. Si se usan fallos de JUAN, estos deben quedar preparados y verificados en el ensayo».

**Directorios y archivos (60 s):** «Administramos objetos DIRECTORY y sus permisos; en Archivos consultamos el DMP y LOG asociado al catálogo, abrimos el registro y mostramos las operaciones con confirmación. No eliminaremos ningún respaldo útil: la eliminación de prueba requiere un DMP descartable, previamente identificado».

**Nota:** cifras de planes (173→107), tiempos de exportación y eventos de auditoría son resultados esperados del guion original; verificar con datos reales en los ensayos y no prometer cifras exactas si cambian.

## Regla de contingencia de tiempo

Si algún proceso tarda más de su tiempo objetivo: mostrar el resultado previamente preparado o la evidencia capturada, explicar el paso y continuar. No iniciar un segundo Data Pump full en vivo si compromete la recuperación. Nunca presentar capturas como ejecución en vivo.

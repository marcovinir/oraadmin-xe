# Recuperación

1. En **Restaurar**, seleccione un respaldo. Solo aparecen los completados cuyo `.DMP` sigue en disco.
2. Elija **Todo el respaldo**, **Algunos esquemas** o **Algunas tablas** y marque lo que va a restaurar.
3. Indique qué hacer **Si la tabla ya existe**. Para no tocar el original, marque **Restaurar en otro esquema** y escriba el destino.
4. Presione **Restaurar**, revise el SQL y presione **Ejecutar**. Al terminar se abre el log; la restauración queda en **Historial**.

Para demostrar una recuperación, presione **Simular desastre**: borra la tabla que elija y deja lista su restauración.

**Figura.** Restauración de un esquema en otro esquema.

**Captura:** `docs/capturas/21_manual_recuperacion.png`

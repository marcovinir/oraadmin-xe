# Tuning

1. En **Plan de ejecución**, escriba una consulta `SELECT` o `WITH` y presione **Plan estimado**. Use **Plan real** para ejecutarla y ver filas y tiempo.
2. Presione **Fijar como ANTES** para guardar ese plan como referencia.
3. En **Índices**, presione **Crear índice…**. En **Estadísticas**, seleccione la tabla y presione **Recolectar (tabla)**, o use **Recolectar (esquema)**.
4. Vuelva a presionar **Plan estimado** y abra **Antes / después** para comparar el costo.

**Figura.** Comparación del plan antes y después del índice.

**Captura:** `docs/capturas/24_manual_tuning.png`

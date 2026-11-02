# Seguimiento de la fase 7

La interfaz MVC de facturas ya permite consultar, crear, editar, calcular y guardar facturas, además de confirmar y ejecutar las transiciones disponibles. Los cálculos y las reglas de negocio permanecen en la API. El incremento F7.1–F7.5 está completado en el alcance HTTP probado; los pasos F7.6–F7.10 siguen pendientes.

| Paso | Estado | Evidencia y alcance |
| --- | --- | --- |
| F7.1 | Completado | Navegación, listado, detalle, búsqueda, enlaces a Crear/Editar y confirmaciones GET de solo lectura. Los POST protegidos ejecutan pago, anulación y eliminación con PRG, errores controlados y consulta del estado ante una respuesta de pago incierta. Regresión actual: 83/83 verificaciones HTTP MVC. |
| F7.2 | Completado | Formularios Crear y Editar cargan clientes, empleados, productos y configuración desde la API; los métodos de pago provienen de la enumeración compartida. La opción de edición conserva los valores históricos por defecto y permite solicitar los actuales. |
| F7.3 | Completado | Agregar, modificar y quitar líneas con claves de enlace estables. Se conserva el Id de detalle, la entrada sin procesar y los errores de cada fila; quitar una fila intermedia no desplaza las restantes. |
| F7.4 | Completado | Campos de servidor informativos; el formulario conserva valores editables y vuelve a cargar catálogos. Los valores de presentación no son autoridad para guardar. |
| F7.5 | Completado | Previsualización de creación y edición por API. La MVC muestra el cálculo devuelto sin recalcular ni alterar existencias o identidades. |
| F7.6–F7.10 | Pendientes | Continúan según el plan original. |

## Verificación del incremento

La comprobación actual suma **172/172 verificaciones HTTP**: 89 del grupo de formularios y 83 del grupo de consultas y transiciones. La regresión de 83 casos conserva el alcance anterior y actualiza la expectativa de que Crear y Editar están disponibles. Los 48 casos parciales de una ejecución diagnóstica no se cuentan.

El prerequisito de cálculo de edición en API tuvo una ejecución separada de 363/363 verificaciones. Ese resultado documenta la API usada por F7.5 y no se suma a las 172 verificaciones MVC.

La compilación de la solución terminó con 0 errores y 0 advertencias. La regresión confirmó formularios y catálogos, filas dinámicas, preservación de entradas inválidas, valores históricos y opción de actualización, previsualización sin efectos, controles contra campos de servidor manipulados, y flujos de consulta y transición.

## Límites

- La API conserva la autoridad sobre estados, referencias, precios, unidades, importes e inventario.
- El resultado de cálculo es informativo hasta que la API confirma el guardado.
- F7.6–F7.10 y la revisión visual en navegador no forman parte de esta entrega.

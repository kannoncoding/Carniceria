# Seguimiento de la fase 7

La interfaz MVC de facturas permite consultar, crear, editar, calcular y guardar facturas, además de confirmar y ejecutar las transiciones disponibles. Los cálculos y las reglas de negocio permanecen en la API. Los diez pasos de F7 están completos en el alcance HTTP verificado; la revisión visual en navegador queda pendiente.

| Paso | Estado | Evidencia y alcance |
| --- | --- | --- |
| F7.1 | Completado | Navegación, listado, detalle, búsqueda, enlaces a Crear/Editar y confirmaciones GET de solo lectura. Los POST protegidos ejecutan pago, anulación y eliminación con PRG, errores controlados y consulta del estado ante una respuesta de pago incierta. Regresión actual: 83/83 verificaciones HTTP MVC. |
| F7.2 | Completado | Formularios Crear y Editar cargan clientes, empleados, productos y configuración desde la API; los métodos de pago provienen de la enumeración compartida. La opción de edición conserva los valores históricos por defecto y permite solicitar los actuales. |
| F7.3 | Completado | Agregar, modificar y quitar líneas con claves de enlace estables. Se conserva el Id de detalle, la entrada sin procesar y los errores de cada fila; quitar una fila intermedia no desplaza las restantes. |
| F7.4 | Completado | Campos de servidor informativos; el formulario conserva valores editables y vuelve a cargar catálogos. Los valores de presentación no son autoridad para guardar. |
| F7.5 | Completado | Previsualización de creación y edición por API. La MVC muestra el cálculo devuelto sin recalcular ni alterar existencias o identidades. |
| F7.6 | Completado | La vista presenta precio unitario en colones, subtotal, descuento, base gravable, IVA y total devueltos por API; conserva cantidades y porcentajes fraccionarios en el formulario y el detalle. Verificación: valores pequeños de cantidad/descuento en las suites HTTP y build posterior al formato monetario. |
| F7.7 | Completado | Búsqueda independiente de Index por número o identificación de cliente, con texto codificado y mensaje de cero resultados. Verificación: 83/83 consultas y transiciones MVC. |
| F7.8 | Completado | Las confirmaciones identifican la factura y las cantidades/unidades afectadas; las acciones solo mutan mediante POST después de confirmar. Verificación: 83/83 consultas y transiciones MVC. |
| F7.9 | Completado | Ante validaciones y fallas de API se conservan cabecera, filas, valores intentados, errores por campo/fila y opciones disponibles. Verificación: 89/89 formularios MVC. |
| F7.10 | Completado | Las acciones mutadoras MVC requieren antifalsificación; las confirmaciones por GET y otras consultas no cambian datos. Verificación: 83/83 consultas y transiciones MVC. |

## Verificación del incremento

La comprobación actual suma **172/172 verificaciones HTTP**: 89 del grupo de formularios y 83 del grupo de consultas y transiciones. La regresión de 83 casos conserva el alcance anterior y actualiza la expectativa de que Crear y Editar están disponibles. Los 48 casos parciales de una ejecución diagnóstica no se cuentan.

El prerequisito de cálculo de edición en API tuvo una ejecución separada de 363/363 verificaciones. Ese resultado documenta la API usada por F7.5 y no se suma a las 172 verificaciones MVC.

La compilación de la solución terminó con 0 errores y 0 advertencias. La regresión confirmó formularios y catálogos, filas dinámicas, preservación de entradas inválidas, valores históricos y opción de actualización, previsualización sin efectos, controles contra campos de servidor manipulados, y flujos de consulta y transición.

## Límites

- La API conserva la autoridad sobre estados, referencias, precios, unidades, importes e inventario.
- El resultado de cálculo es informativo hasta que la API confirma el guardado.
- La revisión visual en navegador sigue pendiente de comprobación manual.
- La regresión global F8 cubre más módulos y queda fuera del alcance de esta fase.

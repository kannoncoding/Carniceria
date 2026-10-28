# Seguimiento de la fase 2

Registro incremental de los ocho pasos de F2. Los estados requieren evidencia de implementación; la fase no se cierra hasta cumplir su salida y las verificaciones previstas.

| Paso | Estado | Evidencia o límite |
| --- | --- | --- |
| F2.1 | Completado | Se copiaron los cinco archivos del almacén desde `Carniceria.Web/Data` a `Carniceria.Api/Datos` y se adaptó el espacio de nombres a `Carniceria.Api.Datos`. Se conservaron las operaciones, el bloqueo compartido y las copias defensivas. La versión de Web permanece intacta para la migración y retirada en F4. La solución compila con 0 errores y 0 advertencias. |
| F2.2 | Pendiente | Registrar el almacén como singleton solo en API cuando se definan sus dependencias. Web conserva temporalmente su registro hasta la conexión de controladores en F4. |
| F2.3 | Pendiente | Definir DTOs de escritura y lectura, errores y mapeos reutilizables por los servicios y controladores posteriores. |
| F2.4 | Pendiente | Agregar la relación obligatoria entre producto y proveedor a contratos y formularios; resolver el periodo de compatibilidad con el almacén temporal de Web sin adelantar cambios de interfaz de F4. |
| F2.5 | Pendiente | Preparar semillas válidas de clientes, empleado, proveedor y productos relacionados. |
| F2.6 | Pendiente | Ejecutar las semillas una vez por inicio de API y en orden de dependencia; agregar la factura cuando exista F5. |
| F2.7 | Pendiente | Definir validación autoritativa para solicitudes directas a API, incluidos rangos, catálogos, relaciones y duplicados. |
| F2.8 | Pendiente | Verificar persistencia entre solicitudes y que las copias leídas no permitan mutar el almacén. T02 será parcial aquí; T03 se cierra al retirar el almacén de Web en F4. |

## Límites de esta etapa

- El almacén duplicado de Web es temporal y se mantiene sin cambios hasta F4, cuando sus controladores migren a HTTP.
- F2 no implementa el CRUD completo de API; ese trabajo corresponde a F3.
- La salida final de F2 requiere datos base accesibles por API y contratos/mapeos definidos. T03 permanece abierta hasta la auditoría de F4.
- No se registró todavía el almacén en API ni se agregaron DTOs, semillas o endpoints de negocio en F2.1.

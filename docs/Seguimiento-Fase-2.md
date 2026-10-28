# Seguimiento de la fase 2

Registro incremental de los ocho pasos de F2. Los estados requieren evidencia de implementación; la fase no se cierra hasta cumplir su salida y las verificaciones previstas.

| Paso | Estado | Evidencia o límite |
| --- | --- | --- |
| F2.1 | Completado | Se copiaron los cinco archivos del almacén desde `Carniceria.Web/Data` a `Carniceria.Api/Datos` y se adaptó el espacio de nombres a `Carniceria.Api.Datos`. Se conservaron las operaciones, el bloqueo compartido y las copias defensivas. La versión de Web permanece intacta para la migración y retirada en F4. La solución compila con 0 errores y 0 advertencias. |
| F2.2 | Completado | `Carniceria.Api/Program.cs` registra `IAlmacenCarniceria` como singleton con `AlmacenCarniceria`. El contenedor de API entrega la misma instancia durante la vida del proceso. El registro del almacén heredado en Web se conserva temporalmente hasta la conexión de controladores en F4. La solución compila con 0 errores y 0 advertencias. |
| F2.3 | Completado | En `Carniceria.Modelos/Contratos` se agregaron `SolicitudCliente`, `SolicitudEmpleado`, `SolicitudProducto` y `SolicitudProveedor`, heredando sus formularios validados y sin exponer Id. Las cuatro respuestas comparten `RespuestaPersona` para cliente/empleado y cubren los campos actuales de las cuatro entidades. `ErrorApi` contiene código, mensaje y errores por campo. Los mapeadores estáticos y sin estado en `Carniceria.Api/Mapeos` convierten entidades a respuestas y resultados de operación a errores. La solución compila con 0 errores y 0 advertencias. ProveedorId y facturas quedan para fases posteriores. |
| F2.4 | Completado | `Producto` incorpora `ProveedorId`; `ProductoFormulario` lo expone como `Guid?` sin `[Required]` para mantener compilable el flujo temporal de Web hasta F4. La creación y actualización en el almacén de API exigen, bajo el mismo bloqueo y antes de mutar, un Id no vacío que corresponda a un proveedor existente; si falta o no existe, devuelven validación fallida asociada a `ProveedorId`. `RespuestaProducto` incluye Id, razón social e identificación del proveedor; el mapeador recibe el proveedor correspondiente y usa `Producto.ProveedorId`. La solución compila con 0 errores y 0 advertencias. No se agregó selector ni se modificó la interfaz de Web. |
| F2.5 | Completado (definidas en F2.5 y activadas en F2.6) | `AlmacenCarniceria.Semillas.cs` define `SembrarDatosIniciales` y crea, mediante las operaciones validadas, un cliente, empleado, proveedor y los productos RES001/POL001. Ambos productos reciben el Id generado del proveedor. Cada resultado exige éxito e Id no vacío; una falla lanza `InvalidOperationException` descriptiva. F2.6 activa el método una vez desde el constructor del almacén. |
| F2.6 | Completado | El constructor de `AlmacenCarniceria` invoca una sola vez `SembrarDatosIniciales`. El registro singleton de API se resuelve al iniciar el programa, inmediatamente después de `Build` y antes de `Run`, para sembrar al inicio y no en la primera solicitud. Build sin errores; humo HTTP en puerto alternativo 5231 inició API, `GET /api/estado` devolvió `Disponible` y los registros no mostraron excepciones de siembra. El proceso se cerró y el puerto quedó libre. No se agregaron archivos de persistencia. Las semillas de factura se añadirán cuando exista F5. |
| F2.7 | Pendiente | Definir validación autoritativa para solicitudes directas a API, incluidos rangos, catálogos, relaciones y duplicados. |
| F2.8 | Pendiente | Verificar persistencia entre solicitudes y que las copias leídas no permitan mutar el almacén. T02 será parcial aquí; T03 se cierra al retirar el almacén de Web en F4. |

## Límites de esta etapa

- El almacén duplicado de Web es temporal y se mantiene sin cambios hasta F4, cuando sus controladores migren a HTTP.
- F2 no implementa el CRUD completo de API; ese trabajo corresponde a F3.
- F2.3 define contratos de las cuatro entidades existentes; los campos de factura corresponden a F5.
- El formulario compartido de producto permite omitir temporalmente `ProveedorId` para el almacén heredado de Web hasta F4; la API valida la relación estrictamente.
- La salida final de F2 requiere datos base accesibles por API y contratos/mapeos definidos. T03 permanece abierta hasta la auditoría de F4.
- F2.1 no agregó DTOs, semillas ni endpoints de negocio. F2.2 registra el singleton en DI. F2.5 define el método de siembra y F2.6 lo activa en el constructor, resuelto de forma anticipada al inicio de API.

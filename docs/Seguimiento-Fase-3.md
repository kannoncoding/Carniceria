# Seguimiento de la fase 3

Registro incremental de las ocho actividades de F3. La migración MVC queda para F4 y los vínculos con facturas para F5-F6.

| Paso | Estado | Evidencia o límite |
| --- | --- | --- |
| F3.1 | Completado (compilación y revisión estática QA) | Controladores API ofrecen listado, consulta por Id, alta, actualización, eliminación y búsqueda para clientes, empleados, productos y proveedores. Rutas de búsqueda explícitas; operaciones con solicitudes/respuestas compartidas y errores `ErrorApi`. La compilación terminó con 0 errores y 0 advertencias. La verificación HTTP queda agrupada en F3.8. |
| F3.2 | Completado | Altas y ediciones reutilizan los formularios y validadores de Modelos; el Id se genera en el servidor y las ediciones usan el Id de ruta. El grupo `Validaciones` de [`Verificar-Api.ps1`](../pruebas/Fase3/Verificar-Api.ps1) aprobó 143/143 verificaciones HTTP: formatos y límites de formularios, catálogos, fechas, rangos, overflow, referencias, duplicados, autoedición, Id enviado en el cuerpo y ausencia de mutaciones tras rechazos. El servidor devolvió `$.fechaNacimiento` como clave de error del model binding; se ajustó el verificador para aceptar el nombre o la ruta JSON exactos, sin cambios de aplicación. QA confirmó la ejecución en 5233 y el cierre del proceso. |
| F3.3 | Implementado; auditoría en F3.8 | Búsqueda de clientes/empleados por identificación; productos por `Codigo` o `Nombre`; proveedores por `Identificacion` o `RazonSocial`. Criterios inválidos retornan 400 y búsquedas vacías retornan lista vacía. |
| F3.4 | Implementado desde F2.4; auditoría en F3.8 | El almacén valida y asigna el proveedor bajo su bloqueo; las respuestas del producto incluyen información descriptiva del proveedor. Web conserva su formulario sin proveedor hasta F4. |
| F3.5 | Parcial adelantado | El almacén bloquea la eliminación de proveedores relacionados con productos mediante `ConflictoIntegridad`/409. Las referencias a clientes, empleados y productos con facturas se completan en F6. |
| F3.6 | Implementado; auditoría en F3.8 | Relaciones y mutaciones compuestas se ejecutan bajo el mismo bloqueo. `EjecutarConBloqueo` permite construir DTOs de respuesta coherentes antes de liberar el bloqueo. |
| F3.7 | Implementado; auditoría en F3.8 | Errores de validación/entrada: 400; recurso inexistente: 404; duplicado o conflicto de integridad: 409. Todos usan `ErrorApi` con mensajes y errores por campo. |
| F3.8 | Pendiente | Extender `pruebas/Fase3/Verificar-Api.ps1` con los grupos restantes y ejecutar la verificación final sin dependencias externas, evitando repetir los casos ya aprobados cuando no cambie su alcance. |

## Límites de F3

- Esta fase prepara los cuatro servicios API; los controladores MVC mantienen temporalmente su almacén hasta F4.
- No se implementan relaciones con facturas en los borrados de F3; su protección completa corresponde a F6.
- Las pruebas directas, carreras, errores HTTP y repetición tras cambios se registrarán en F3.8 con evidencia reproducible.
- El conflicto de eliminación de proveedores se adelantó para evitar crear una ruta pública que dejara productos sin proveedor.

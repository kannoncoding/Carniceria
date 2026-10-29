# Seguimiento de la fase 4

Registro incremental de la migración MVC al consumo de la API. La fase permanece abierta hasta verificar que Web no conserva almacén propio y que los datos sobreviven al reinicio de Web, pero no al de API.

| Paso | Estado | Evidencia o límite |
| --- | --- | --- |
| F4.1 | Completado | Los cuatro controladores MVC usan `IHttpClientFactory` y acciones asíncronas para sus operaciones de datos. El cliente nombrado `ApiCarniceria` conserva la dirección base configurable y el límite de tres segundos ya registrado. Se usan los contratos `Respuesta*` para lecturas y los formularios compartidos para escrituras. La compilación de `Carniceria.slnx` terminó con 0 errores y 0 advertencias. El humo [`Verificar-Mvc.mjs`](../pruebas/Fase4/Verificar-Mvc.mjs) aprobó 77/77 aserciones. Una prueba dirigida posterior confirmó el cierre del harness y que los puertos 5234/5235 quedaron sin listeners; no fue necesario repetir las 77 aserciones. |
| F4.2 | Implementado; auditoría pendiente | Listado, detalles, búsqueda, creación, edición y confirmación/eliminación conservan las rutas MVC existentes y consultan los recursos API. Las búsquedas se envían como parámetros URL codificados y sus filtros permanecen en API. |
| F4.3 | Implementado; auditoría pendiente | El formulario de producto carga proveedores desde API en alta y edición; el Id del proveedor acompaña el formulario. Los errores y fallos de transporte conservan el formulario y el proveedor seleccionado; los datos descriptivos se muestran en inventario, detalles y resultados de búsqueda. |
| F4.4 | Implementado; auditoría pendiente | Los errores `ErrorApi` se normalizan a nombres de campo del formulario. Las claves desconocidas se muestran en el resumen; se manejan claves JSON como `$.fechaNacimiento` y prefijos `solicitud.`. |
| F4.5 | Implementado; auditoría pendiente | Las mutaciones confirmadas redirigen con mensaje de éxito. Los errores de alta/edición mantienen los valores capturados. La eliminación bloqueada por 409 conserva la página de confirmación y carga un DTO actual antes de volver a mostrarla; 404 representa únicamente un recurso ausente. |
| F4.6 | Implementado; auditoría pendiente | Errores de conexión, timeout y respuestas JSON nulas, malformadas o incompletas no se presentan como listas vacías ni como éxitos. Las búsquedas conservan criterio y consulta. Si una mutación ya se envió y no se recibe confirmación, el formulario indica que el resultado es incierto y no reintenta. |
| F4.7 | Pendiente de verificación funcional | Se conserva el binder decimal de `es-CR`, los campos de fecha, turnos, puestos dependientes del área y alertas de inventario. Falta comprobar su ciclo HTTP MVC, incluida la serialización decimal como número JSON. |
| F4.8 | Pendiente | Retirar `IAlmacenCarniceria`, sus colecciones y su registro de Web después de aprobar las operaciones MVC; comprobar que controladores, vistas, JavaScript y configuración no mantienen datos de negocio ni llamadas directas desde el navegador. |
| F4.9 | Pendiente | Crear datos mediante MVC, reiniciar solo Web y comprobar que permanecen en API; reiniciar API y comprobar que desaparecen las altas y reaparecen únicamente las semillas. |

## Límite de la evidencia actual

El humo inicial cubre interacciones MVC HTTP, no la prueba de cultura/interfaz interactiva ni el reinicio aislado de Web y API. Las auditorías de F4.2–F4.6 y la prueba de cultura de F4.7 siguen pendientes. F4.8 y F4.9 no se consideran completados; la eliminación de dependencias Web al almacén y la propiedad única del almacenamiento se comprobarán allí.

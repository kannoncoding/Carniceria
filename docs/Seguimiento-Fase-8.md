# Seguimiento de la fase 8

F8 registra la ejecución actual de las suites T01–T32 disponibles, añade escenarios de formularios obsoletos tras reiniciar API y valida un paquete candidato limpio. T01 se sustenta en la confirmación manual del usuario; T31–T32 se probaron en un candidato técnico, no en el paquete final de F10.

| Paso | Estado | Evidencia y límite |
| --- | --- | --- |
| F8.1 | Completado en el alcance probado | Se ejecutó T02–T30 con las suites actuales disponibles; T01 proviene de la confirmación manual del usuario sobre inicio dual. T31–T32 se probaron en ZIP candidato extraído: restore/build 0/0, smoke MVC 83/83, inspección de archivos y arranque dual. La matriz especifica datos, pasos, resultados y evidencia. F10 debe validar el comprimido definitivo. |
| F8.2 | Completado | La automatización existente se concentra en validadores/modelos, contratos y operaciones críticas de factura, existencias, transiciones, atomicidad, concurrencia y MVC. No se añadieron paquetes de prueba externos. Las ejecuciones de F1–F7 se identifican como evidencia de sus fases. |
| F8.3 | Completado | La cobertura directa de API y los recorridos MVC se identifican por caso en la matriz. Las verificaciones de campos de servidor manipulados, referencias, límites e integridad se mantienen en API; la UI se verifica mediante sus controladores MVC. |
| F8.4 | Completado | `pruebas/Fase8/Verificar-FallosReinicioMvc.mjs` aprobó 13/13 casos dirigidos sobre fallo real de API, formulario obsoleto y reinicio. Incluye colección vacía, pérdida de respuesta de alta, IDs de catálogo vencidos y edición de una factura desaparecida. Todos los procesos y puertos propios terminaron cerrados. |

## Matriz de pruebas T01–T32

Las filas identifican las suites actuales que cubren cada requisito; no implican que cada dato del plan equivalga a una aserción independiente. F8 no declara una comprobación nueva en Visual Studio para T01: el usuario confirmó el inicio dual manualmente. T31–T32 y F8.4 se reportan separadas del total de la regresión por fases.

| Caso | Datos iniciales | Procedimiento y resultado esperado | Resultado y evidencia |
| --- | --- | --- | --- |
| T01 | Solución con perfil compartido. | Iniciar Web y API desde Visual Studio; ambas deben responder por HTTP y Web debe leer API. | Confirmada manualmente por el usuario después de seguir la guía de inicio dual y de factura; no es una ejecución de QA en F8. La configuración base se documentó en [`Seguimiento-Fase-1.md`](Seguimiento-Fase-1.md), F1.6–F1.8. |
| T02 | Almacén recién iniciado. | Reiniciar API; deben reaparecer cliente, empleado, proveedor, dos productos y factura Pendiente de dos líneas; stock intacto. | Aprobada en regresión F8: [`Verificar-SemillasHttp.mjs`](../pruebas/Fase5/PreparacionFactura/Verificar-SemillasHttp.mjs), incluida en F5 36/36. |
| T03 | Registro creado desde MVC. | Reiniciar Web y consultarlo; después reiniciar API y consultar: sobrevive al primero, se pierde al segundo y regresan semillas. | Aprobada en regresión F8: [`Seguimiento-Fase-4.md`](Seguimiento-Fase-4.md), `Verificar-Reinicios.mjs` 58/58. F8.4 también comprueba IDs obsoletos. |
| T04 | Clientes de prueba, incluidos duplicado e inválidos. | Probar CRUD, identificación, textos, fechas e Id; rechazar duplicado/formato inválido y no alterar Id. | Aprobada en regresión F8: F3 API 857/857 y F4 MVC Migracion, parte de 77/77. |
| T05 | Empleado y datos salariales dentro/fuera de límites. | Probar CRUD y salario -1, 0, 5 000 000 y 5 000 001; aceptar 0/límite, rechazar extremos y puesto incompatible; turno visible. | Aprobada en regresión F8: F3 API 857/857 y F4 MVC Migracion, parte de 77/77. La interfaz visual es evidencia previa, no se repitió. |
| T06 | Proveedores válidos e inválidos. | Probar CRUD, tipos de identificación, teléfono, correo y duplicados; rechazar formato/referencia inválida. | Aprobada en regresión F8: F3 API 857/857 y F4 MVC Migracion, parte de 77/77. El grupo F4 Proveedores no se repitió. |
| T07 | Productos con proveedor existente y cantidades variadas. | Probar CRUD, código duplicado, precio cero/positivo, stock negativo, decimales y nivel mínimo. | Aprobada en regresión F8: F3 API 857/857 y F4 MVC Migracion, parte de 77/77. La interfaz visual es evidencia previa. |
| T08 | Clientes con cero, una o múltiples facturas; consultas inexistentes. | Buscar cada módulo por sus criterios y mostrar lista correcta o mensaje vacío en página independiente. | Aprobada en regresión F8: F4 MVC Migracion 77/77 y smoke F7 consultas/transiciones del candidato 83/83. |
| T09 | Producto con proveedor válido, inexistente y cambiado. | Crear/editar con cada referencia; rechazar inválida y reflejar proveedor nuevo al aceptar una existente. | Aprobada en regresión F8: F3 API 857/857 y F4 MVC Migracion 77/77; el grupo F4 Proveedores no se repitió. |
| T10 | Entidades con referencias y registros libres. | Intentar bajas relacionadas y libres; bloquear relaciones y permitir registros sin referencias. | Aprobada en regresión F8: F3/F4 para catálogos; F6, T10, para relaciones de factura. |
| T11 | Cliente, empleado, productos y stock suficiente. | Crear factura de dos líneas; API asigna número, fecha e IDs, estado Pendiente y no descuenta stock. | Aprobada en regresión F8: `pruebas/Fase5/PreparacionFactura/Verificar-FacturasHttp.mjs` y `Verificar-FormulariosMvc.mjs`. |
| T12 | Solicitudes sin líneas, con cantidades inválidas o relaciones ausentes. | Enviar alta; rechazar completa con motivo y sin factura parcial. | Aprobada en regresión F8: `pruebas/Fase5/PreparacionFactura/Verificar-FacturasHttp.mjs` y `Verificar-LimitesFacturaHttp.mjs`. |
| T13 | Porcentajes -0,01, 0, 25 y 25,01. | Crear/previsualizar; aceptar extremos válidos, rechazar fuera de rango y aplicar descuento antes de IVA. | Aprobada en regresión F8: `Verificar-FacturasHttp.mjs` y `Verificar-LimitesFacturaHttp.mjs`. |
| T14 | Productos RES001/POL001; descuento 10 %. | Calcular ejemplo; subtotal 11 000, descuento 1 100, base 9 900, IVA 1 287, total 11 187. | Aprobada en F8: F5 Facturas 173/173 y Previsualizacion 87/87. El smoke F7 de 83/83 revisó presentación MVC. |
| T15 | Producto con stock límite y cantidades decimales. | Probar fracción, demanda exacta y exceso; aceptar fracción/límite y rechazar exceso. | Aprobada en regresión F8: `Verificar-FacturasHttp.mjs` y `Verificar-LimitesFacturaHttp.mjs`. |
| T16 | Stock 10 y dos líneas repetidas por 6 y 5. | Registrar demanda agregada 11; debe rechazarse aunque cada línea por sí sola quepa en stock. | Aprobada en regresión F8: `Verificar-FacturasHttp.mjs`. |
| T17 | Alta con primera línea válida y segunda inválida. | Enviar solicitud; no debe persistir factura ni consumir secuencia. | Aprobada en regresión F8: `Verificar-LimitesFacturaHttp.mjs` y preservación F7.9. |
| T18 | Solicitud manipulada con precios/unidad/fecha/número/estado/total. | Alterar campos de servidor; API debe ignorarlos y crear Pendiente con datos autoritativos. | Aprobada en regresión F8: `Verificar-FacturasHttp.mjs` y formularios F7. |
| T19 | Factura Pendiente cantidad 3; stock 10. | Pagar y repetir pago; stock queda 7 y repetición no lo cambia. | Aprobada en regresión F8: F6, T19. |
| T20 | Dos Pendientes compiten por stock insuficiente para ambas. | Pagar concurrentemente; solo una debe descontar, la otra conserva Pendiente y stock no es negativo. | Aprobada en regresión F8: F6, T20/F6.9. |
| T21 | Factura Pagada cantidad 3; stock restante 7. | Editar a 5, repetir y reducir a 2; verificar ajustes 5, 5 y 8. | Aprobada en regresión F8: F6, T21, y edición MVC F7. |
| T22 | Factura Pagada con productos A/B. | Quitar A y agregar B; reintegrar/descontar en una operación íntegra. | Aprobada en regresión F8: F6, T22, y formulario MVC F7. |
| T23 | Factura Pagada con cantidad 2 y stock 8. | Anular, repetir y editar Anulada; stock 10 una vez y edición rechazada. | Aprobada en regresión F8: F6, T23. |
| T24 | Facturas Pendiente, Pagada y Anulada. | Intentar bajas y retrocesos; solo Pendiente se elimina, sin modificar stock. | Aprobada en regresión F8: F6, T24; confirmaciones MVC F7.8. |
| T25 | Edición Pagada con fallo en una línea. | Provocar stock insuficiente o overflow tardío; factura, importes y stock quedan intactos. | Aprobada en regresión F8: F6, T25, `Verificar-AtomicidadHttp.mjs`. |
| T26 | Solicitudes repetidas/concurrentes y edición sin cambios. | Competir y repetir operaciones; no hay doble ajuste, doble reintegro ni stock negativo. | Aprobada en regresión F8: F6, T26, `Verificar-ConcurrenciaHttp.mjs`. |
| T27 | Formulario con varias líneas y una intermedia inválida/removida. | Agregar/quitar y provocar error; conservar cabecera, filas, claves, opciones y error correcto. | Aprobada en regresión F8: F7, `Verificar-FormulariosMvc.mjs` 89/89. |
| T28 | Confirmaciones de baja, pago y anulación. | Cancelar cada confirmación; no debe cambiar datos ni stock. | Aprobada en regresión F8: F7, `Verificar-ConsultasTransicionesMvc.mjs` 83/83. |
| T29 | API ausente/error/timeout y formulario con entradas. | Consultar o guardar; mensaje controlado y datos conservados; tras resultado incierto de pago consultar estado. | F4 transporte es evidencia previa. El smoke F7 del candidato 83/83 verifica respuesta de pago perdida. Además, aprobada F8: [`Verificar-FallosReinicioMvc.mjs`](../pruebas/Fase8/Verificar-FallosReinicioMvc.mjs), parte de 13/13. |
| T30 | Menú, rutas, fechas, moneda y mensajes. | Recorrer módulos, verificar emisión de solo lectura y presentación uniforme. | Aprobada en regresión F8: suites F4 y F7. |
| T31 | ZIP candidato extraído en carpeta limpia. | Restaurar, compilar e iniciar Web/API; deben iniciar y el smoke MVC responder. | Aprobada F8 sobre candidato: restore/build 0/0 y smoke MVC 83/83; no es el ZIP final F10. |
| T32 | Contenido del ZIP candidato. | Inspeccionar archivos, directorios y dependencias; sin bin/obj/.git/.vs ni paquetes externos. | Aprobada F8: 200 archivos, 2 directorios, cero `PackageReference`; `Carniceria.slnLaunch` se preservó literalmente. |

## Ejecuciones nuevas F8

La regresión F8 actual suma **2 249 aserciones** aprobadas en 25 grupos distintos: F3 857/857; F5 333/333 (configuración 6, operaciones 173, semillas 36, vista previa 87 y límites 31); F6 739/739; F4 reinicios 58/58 y migración MVC 77/77; F7 formularios 89/89; candidato T31 smoke 83/83; F8.4 13/13. Son grupos distintos de esta corrida y el smoke del candidato se conserva separado. T01 se considera confirmada por el reporte manual del usuario “perfecto todos los checks” después de configurar el inicio dual; no se presenta como una verificación de Visual Studio realizada por QA.

T31–T32 se ejecutaron sobre un candidato técnico de **SHA-256 `0E364C59DD438DFE87D84C1882FE62F0CC698B2E6E3D4572774DC742B1EF4822`**. El ZIP no se versiona ni se declara entrega final; F10 debe volver a validar el paquete definitivo. Restore/build en extracción terminó con 0 errores y 0 advertencias; el smoke de Consultas y Transiciones MVC aprobó 83/83.

F8.4 aprobó **13/13**: colección vacía, API apagada durante alta con formulario y datos preservados, reinicio que regenera IDs, alta con IDs obsoletos rechazada sin escritura, y edición de factura desaparecida respondida como 404 sin alterar la semilla. El verificador está disponible en [`Verificar-FallosReinicioMvc.mjs`](../pruebas/Fase8/Verificar-FallosReinicioMvc.mjs). El candidato y las evidencias crudas quedan fuera del repositorio; el hash permite identificar el archivo probado sin documentar una ruta personal.

## Límites

Las suites actuales cubrieron T02–T30 en los grupos indicados; no se afirma que cada fila del plan tenga una aserción individual ni que las 2 249 verificaciones sean una única suite. El candidato de T31–T32 es técnico, no el paquete definitivo. La matriz no sustituye la revisión visual ni la regresión final de F10.

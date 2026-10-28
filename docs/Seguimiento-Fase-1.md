# Seguimiento de la fase 1

Registro de implementación de F1.1 a F1.7. Los estados reflejan evidencia disponible; preparar un archivo de perfil no equivale a verificar el inicio simultáneo desde Visual Studio.

| Paso | Estado | Evidencia |
| --- | --- | --- |
| F1.1 | Completado | Commit `cb2cd6c`: API ASP.NET Core con controladores en .NET 10, perfil HTTP y endpoint mínimo `GET /api/estado`; agregada a `Carniceria.slnx`. |
| F1.2 | Completado | Commit `954729c`: API y Web referencian Modelos; no hay referencias entre API y Web. |
| F1.3 | Completado | Commit `f7543de`: Web HTTP en 5220, API HTTP en 5221 y dirección base configurable en `Api:DireccionBase`. |
| F1.4 | Completado | Commit `86f57e8`: retirados HSTS, redirección HTTPS y perfil HTTPS de Web; se conserva el manejo de excepciones. Ambos proyectos tienen perfil HTTP. |
| F1.5 | Completado | Commit `90f9c95`: la página inicial de MVC consulta el estado de API con `IHttpClientFactory`; QA aprobó la respuesta real y los casos de API apagada, HTTP 500, JSON inválido, esquema inesperado, cuerpo truncado y timeout. La compilación de la solución terminó con 0 errores y 0 advertencias. |
| F1.6 | Parcial | `Carniceria.slnLaunch` es un candidato generado por código. Falta abrirlo/guardarlo y verificarlo en Visual Studio Community 2026 mediante el flujo descrito abajo. |
| F1.7 | Completado | `dotnet list Carniceria.slnx package --include-transitive --no-restore --verbosity normal` terminó correctamente: los tres proyectos `net10.0` informan que no hay paquetes para el framework. La inspección de los tres `.csproj` no encontró `PackageReference`; los tres `project.assets.json` no contienen bibliotecas de tipo `package`. No hay dependencias NuGet externas agregadas que retirar. |
| F1.8 | Pendiente | Verificar compilación y ejecución simultánea con F5 en Visual Studio, incluyendo respuesta de MVC y prueba desde una copia limpia. |

**T01 sigue pendiente.** No se ha confirmado el inicio simultáneo desde el perfil compartido de Visual Studio ni su verificación desde una copia limpia. La fase 1 no está cerrada.

## Guardar el perfil compartido desde Visual Studio 2026

El archivo `Carniceria.slnLaunch` incluido en esta entrega fue preparado por código con el formato de perfil compartido y no ha sido generado ni guardado por el IDE. Completar estos pasos en Visual Studio Community 2026:

1. Abrir `Carniceria.slnx`.
2. Hacer clic derecho en la solución y elegir **Configure Startup Projects...** (o **Configurar proyectos de inicio...**).
3. En **Common Properties / Propiedades comunes**, seleccionar **Multiple startup projects / Varios proyectos de inicio**.
4. Asignar **Start / Iniciar** a `Carniceria.Api` y `Carniceria.Web`; dejar los demás proyectos sin iniciar.
5. Elegir **Share Profile / Compartir perfil** y aplicar con **Apply / Aplicar** o **OK / Aceptar**.
6. Confirmar que el perfil compartido **Web y API** aparece seleccionado y que ambos proyectos usan sus perfiles `http`.
7. Guardar la solución. Comprobar que el IDE produjo o actualizó `Carniceria.slnLaunch` junto a `Carniceria.slnx`; revisar el archivo resultante y confirmar que conserva las rutas relativas y las acciones de inicio.
8. En una copia limpia de la carpeta del proyecto, abrir la solución, seleccionar **Web y API** e iniciar con F5. Confirmar que Web responde en HTTP y muestra el estado obtenido de API, y detener ambos procesos al terminar.

Si Visual Studio solicita habilitar **Enable Multi-Project Launch Profiles**, habilitar esa característica siguiendo el aviso del IDE y repetir la configuración. El perfil candidato contiene API y Web con la acción `Start`; el guardado en el IDE, la verificación de la copia limpia y T01 permanecen pendientes hasta contar con evidencia de esas acciones.

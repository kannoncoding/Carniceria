# Seguimiento de la fase 1

Registro de implementación y verificación de F1.1 a F1.8. Se distingue la prueba automatizada DTE de la configuración manual y ejecución con F5.

| Paso | Estado | Evidencia |
| --- | --- | --- |
| F1.1 | Completado | Commit `cb2cd6c`: API ASP.NET Core con controladores en .NET 10, perfil HTTP y endpoint mínimo `GET /api/estado`; agregada a `Carniceria.slnx`. |
| F1.2 | Completado | Commit `954729c`: API y Web referencian Modelos; no hay referencias entre API y Web. |
| F1.3 | Completado | Commit `f7543de`: Web HTTP en 5220, API HTTP en 5221 y dirección base configurable en `Api:DireccionBase`. |
| F1.4 | Completado | Commit `86f57e8`: retirados HSTS, redirección HTTPS y perfil HTTPS de Web; se conserva el manejo de excepciones. Ambos proyectos tienen perfil HTTP. |
| F1.5 | Completado | Commit `90f9c95`: la página inicial de MVC consulta el estado de API con `IHttpClientFactory`; la verificación cubrió respuesta real, API apagada, HTTP 500, JSON inválido, esquema inesperado, cuerpo truncado y timeout. La compilación de la solución terminó con 0 errores y 0 advertencias. |
| F1.6 | Completado | El 2026-10-28, la captura de Visual Studio mostró `MultipleStartupProjects`, API y Web en `Start`, Modelos en `None`, `Share Profile` seleccionado y el perfil `Web y API` elegido. Se verificó que `Carniceria.slnLaunch` en la solución original y en la copia limpia tienen el mismo contenido JSON normalizado. |
| F1.7 | Completado | `dotnet list Carniceria.slnx package --include-transitive --no-restore --verbosity normal` terminó correctamente: los tres proyectos `net10.0` informan que no hay paquetes para el framework. La inspección de los tres `.csproj` no encontró `PackageReference`; los tres `project.assets.json` no contienen bibliotecas de tipo `package`. No hay dependencias NuGet externas agregadas que retirar. |
| F1.8 | Completado | La prueba manual con F5 en copia limpia mostró API por HTTP 5221 y Web por HTTP 5220; ambas páginas funcionaron. Evidencia DTE anterior e independiente: `Debug.Start` mostró API `Disponible` y MVC HTTP 200 con el mensaje exacto de disponibilidad. |

**T01 aprobada el 2026-10-28. Fase 1 cerrada.** La configuración compartida se verificó en Visual Studio y la ejecución con F5 se comprobó en la copia limpia. No se registró la detención de la sesión manual.

La copia limpia de F1.8 se creó en una ubicación local que no forma parte del repositorio; su ruta personal se omite en esta guía. La comprobación manual con F5 usó el perfil compartido. La configuración automática DTE descrita más abajo es una comprobación anterior e independiente.

### Preparación y evidencia automatizada de la copia limpia

La copia se preparó desde la raíz del repositorio con `robocopy` excluyendo `.git`, `.vs`, `bin`, `obj` y `output`. Antes de abrir el IDE no contenía `.git`, `.vs`, `bin` ni `obj`. Solo en la copia se modificó `Carniceria.Web/Properties/launchSettings.json` para que el perfil `http` tuviera `launchBrowser: false`; el perfil de la carpeta original no se cambió. Para repetir el restore y la compilación, asigne `$rutaCopia` a la ruta de una copia limpia:

```powershell
$rutaCopia = 'C:\ruta\local\F1.8-Copia-Limpia'
dotnet restore (Join-Path $rutaCopia 'Carniceria.slnx')
dotnet build (Join-Path $rutaCopia 'Carniceria.slnx') --no-restore --disable-build-servers -m:1 -v:minimal
```

`dotnet restore` terminó con código 0 y restauró Web, Modelos y API. La compilación terminó con 0 errores y 0 advertencias.

Antes de la confirmación manual, el inicio se automatizó con `powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File iniciar-y-verificar.ps1` desde la carpeta de evidencia. La política `Bypass` se aplicó solo a ese proceso; no se cambió la política persistente del equipo. El verificador decodificó el HTML y comparó la frase positiva exacta `El servicio de datos está disponible.`. La transcripción sanitizada conserva los resultados observados; el valor local de `Solucion` se sustituyó por `<ruta de la copia verificada>`. El script y sus registros detallados de QA permanecen en una ubicación local no incluida en el repositorio:

```text
Version=18.0
Solucion=<ruta de la copia verificada>
ProyectosInicio configurados.
Debug.Start solicitado.
RespuestaApi=Disponible
EstadoHttpMvc=200
TipoContenidoMvc=text/html; charset=utf-8
MensajeMvc=El servicio de datos está disponible.
RespuestaCapturada=True
MvcMuestraEstado=True
```

El bloque `finally` del script DTE ejecutó `Debug.StopDebugging` y `Quit`. Después de esa prueba automatizada, una consulta no encontró procesos `devenv` ni listeners en 5220/5221. Visual Studio creó `.vs` y `bin`/`obj` se generaron durante la restauración y compilación; su ausencia se comprobó al crear la copia, no después de ejecutar la verificación. Esta observación se refiere solo a la prueba DTE, no a los procesos del inicio manual posterior.

## Flujo de perfil compartido para repetir la verificación

La ruta de configuración corresponde a [Microsoft Learn: configurar varios proyectos de inicio](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects?view=visualstudio). Estos son los pasos usados para la verificación manual:

1. Abrir `Carniceria.slnx`.
2. Hacer clic derecho en la solución y elegir **Configure Startup Projects...** (o **Configurar proyectos de inicio...**).
3. En **Common Properties / Propiedades comunes**, seleccionar **Multiple startup projects / Varios proyectos de inicio**.
4. Asignar **Start / Iniciar** a `Carniceria.Api` y `Carniceria.Web`; dejar los demás proyectos sin iniciar.
5. Elegir **Share Profile / Compartir perfil** y aplicar con **Apply / Aplicar** o **OK / Aceptar**.
6. Confirmar que el perfil compartido **Web y API** aparece seleccionado y que ambos proyectos usan sus perfiles `http`.
7. Guardar la solución. Comprobar que el IDE produjo o actualizó `Carniceria.slnLaunch` junto a `Carniceria.slnx`; revisar el archivo resultante y confirmar que conserva las rutas relativas y las acciones de inicio.
8. En una copia limpia de la carpeta del proyecto, abrir la solución, seleccionar **Web y API** e iniciar con F5. Confirmar que Web y API responden por HTTP en los puertos configurados.

Si Visual Studio solicita habilitar **Enable Multi-Project Launch Profiles**, habilitar esa característica siguiendo el aviso del IDE y repetir la configuración. Para la ejecución manual del 2026-10-28, la captura mostró API y Web en `Start`, Modelos en `None`, `Share Profile` activado y `Web y API` seleccionado. Se verificó que los archivos `Carniceria.slnLaunch` de la solución original y de la copia limpia tienen el mismo contenido JSON normalizado.

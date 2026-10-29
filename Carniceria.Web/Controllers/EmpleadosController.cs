using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Carniceria.Modelos.Consultas;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Validacion;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Carniceria.Controllers;

// Atiende las páginas MVC de consulta y mantenimiento de empleados.
public class EmpleadosController : ControladorApiMvcBase
{
    // Recibe la fábrica del empleado HTTP usado por las acciones.
    public EmpleadosController(IHttpClientFactory fabricaEmpleadosHttp)
        : base(fabricaEmpleadosHttp)
    {
    }

    [HttpGet]
    // Consulta la colección de empleados en la API.
    public async Task<IActionResult> Index()
    {
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                "api/empleados", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudo cargar el listado de empleados.");

            // Convertir solo un cuerpo JSON con la colección esperada.
            var empleados = await LeerRespuestaAsync<RespuestaEmpleado[]>(respuesta, cancelacion);
            if (empleados is null || empleados.Any(registro => registro is null || !RespuestaValida(registro)))
                return ServicioNoDisponible("La API respondió con datos de empleados inesperados.");

            return View(empleados);
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("La API respondió con datos de empleados inesperados.");
        }
    }

    [HttpGet]
    // Consulta un empleado por el identificador de la ruta.
    public async Task<IActionResult> Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del empleado no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/empleados/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El empleado solicitado no existe o ya fue eliminado.");
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudieron consultar los datos del empleado.");
            // Confirmar que la respuesta corresponde al Id solicitado.
            var registro = await LeerRespuestaAsync<RespuestaEmpleado>(respuesta, cancelacion);
            return registro is null || !RespuestaValida(registro) || registro.Id != id
                ? ServicioNoDisponible("La API respondió con datos de empleado inesperados.")
                : View(registro);
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("La API respondió con datos de empleado inesperados.");
        }
    }

    [HttpGet]
    // Presenta el formulario vacío para registrar un empleado.
    public IActionResult Crear() => View(new EmpleadoFormulario());

    [HttpPost]
    // Valida el formulario y envía el alta a la API.
    public async Task<IActionResult> Crear([FromForm] EmpleadoFormulario formulario)
    {
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.PostAsJsonAsync("api/empleados", formulario, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.Created)
            {
                // Verificar el DTO antes de anunciar que el alta se completó.
                var registro = await LeerRespuestaAsync<RespuestaEmpleado>(respuesta, cancelacion);
                if (registro is null || !RespuestaValida(registro))
                {
                    AgregarErrorMutacionNoConfirmada();
                    return View(formulario);
                }
                TempData["MensajeExito"] = "El empleado se registró correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresApi(await LeerErrorApiAsync(respuesta, cancelacion), typeof(EmpleadoFormulario));
                return View(formulario);
            }
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (HttpRequestException)
        {
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
    }

    [HttpGet]
    // Busca empleados por identificación y conserva el criterio ingresado.
    public async Task<IActionResult> Buscar([FromQuery] string? consulta)
    {
        // Conservar el texto ingresado y el criterio fijo del módulo.
        var modelo = new BusquedaViewModel<RespuestaEmpleado>
        {
            Criterio = "Identificacion",
            Consulta = ReglasTexto.Normalizar(consulta)
        };
        if (!Request.Query.ContainsKey(nameof(consulta)))
        {
            return View(modelo);
        }
        if (modelo.Consulta.Length == 0)
        {
            ModelState.AddModelError(nameof(modelo.Consulta), "Ingrese una identificación para buscar.");
            return View(modelo);
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            string ruta = $"api/empleados/buscar?identificacion={Uri.EscapeDataString(modelo.Consulta)}";
            using var respuesta = await clienteHttp.GetAsync(ruta, HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "No se pudo consultar la API de empleados.");
                return View(modelo);
            }
            var resultados = await LeerRespuestaAsync<RespuestaEmpleado[]>(respuesta, cancelacion);
            if (resultados is null || resultados.Any(registro => registro is null || !RespuestaValida(registro)))
            {
                ModelState.AddModelError(string.Empty, "La API respondió con resultados de empleados inesperados.");
                return View(modelo);
            }
            modelo.Resultados = resultados;
            modelo.BusquedaRealizada = true;
            return View(modelo);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo consultar la API de empleados.");
            return View(modelo);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "La búsqueda de empleados tardó demasiado en responder.");
            return View(modelo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            ModelState.AddModelError(string.Empty, "La API respondió con resultados de empleados inesperados.");
            return View(modelo);
        }
    }

    [HttpGet]
    // Carga el registro y prepara sus campos editables.
    public async Task<IActionResult> Editar([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del empleado no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/empleados/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El empleado solicitado no existe o ya fue eliminado.");
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los datos para editar el empleado.");
            }
            // Mapear los datos personales y laborales al formulario.
            var registro = await LeerRespuestaAsync<RespuestaEmpleado>(respuesta, cancelacion);
            if (registro is null || !RespuestaValida(registro) || registro.Id != id)
                return ServicioNoDisponible("La API respondió con datos de empleado inesperados.");
            return View(new EmpleadoFormulario
            {
                TipoIdentificacion = registro.TipoIdentificacion,
                Identificacion = registro.Identificacion,
                Nombre = registro.Nombre,
                PrimerApellido = registro.PrimerApellido,
                SegundoApellido = registro.SegundoApellido,
                FechaNacimiento = registro.FechaNacimiento,
                SalarioMensual = registro.SalarioMensual,
                FechaIngreso = registro.FechaIngreso,
                AreaTrabajo = registro.AreaTrabajo,
                Puesto = registro.Puesto,
                Turno = registro.Turno
            });
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("La API respondió con datos de empleado inesperados.");
        }
    }

    [HttpPost]
    // Valida los cambios y actualiza el empleado indicado.
    public async Task<IActionResult> Editar([FromRoute] Guid id, [FromForm] EmpleadoFormulario formulario)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del empleado no contiene un identificador válido.");
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            // Enviar los campos editables sin sustituir el Id de la ruta.
            using var respuesta = await clienteHttp.PutAsJsonAsync($"api/empleados/{id:D}", formulario, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.OK)
            {
                var registro = await LeerRespuestaAsync<RespuestaEmpleado>(respuesta, cancelacion);
                if (registro is null || !RespuestaValida(registro) || registro.Id != id)
                {
                    AgregarErrorMutacionNoConfirmada();
                    return View(formulario);
                }
                TempData["MensajeExito"] = "El empleado se actualizó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El empleado solicitado no existe o ya fue eliminado.");
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresApi(await LeerErrorApiAsync(respuesta, cancelacion), typeof(EmpleadoFormulario));
                return View(formulario);
            }
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (HttpRequestException)
        {
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
    }

    [HttpGet]
    // Presenta los datos antes de confirmar la eliminación.
    public async Task<IActionResult> Eliminar([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del empleado no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/empleados/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El empleado solicitado no existe o ya fue eliminado.");
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los datos para confirmar la eliminación.");
            }
            var registro = await LeerRespuestaAsync<RespuestaEmpleado>(respuesta, cancelacion);
            return registro is null || !RespuestaValida(registro) || registro.Id != id
                ? ServicioNoDisponible("La API respondió con datos de empleado inesperados.")
                : View(registro);
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("La API respondió con datos de empleado inesperados.");
        }
    }

    [HttpPost]
    // Solicita la eliminación confirmada y conserva la confirmación si falla.
    public async Task<IActionResult> EliminarConfirmado([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del empleado no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            // Solicitar una única baja y esperar el resultado del servicio.
            using var respuesta = await clienteHttp.DeleteAsync($"api/empleados/{id:D}", cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NoContent)
            {
                TempData["MensajeExito"] = "El empleado se eliminó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El empleado solicitado no existe o ya fue eliminado.");
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
            {
                var error = await LeerErrorApiAsync(respuesta, cancelacion);
                AgregarErroresApi(error, typeof(RespuestaEmpleado));
                using var consulta = await clienteHttp.GetAsync($"api/empleados/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
                if (consulta.StatusCode == HttpStatusCode.NotFound)
                {
                    return this.RegistroNoEncontrado("El empleado solicitado no existe o ya fue eliminado.");
                }
                if (!consulta.IsSuccessStatusCode)
                {
                    return ServicioNoDisponible("No se pudo volver a cargar la confirmación de eliminación.");
                }
                var registro = await LeerRespuestaAsync<RespuestaEmpleado>(consulta, cancelacion);
                return registro is null || !RespuestaValida(registro) || registro.Id != id
                    ? ServicioNoDisponible("La API respondió con datos de empleado inesperados.")
                    : View("Eliminar", registro);
            }
            return ServicioNoDisponible("No se pudo confirmar la eliminación del empleado.");
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(cancelacion, true);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(cancelacion, true);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("No se pudo confirmar la eliminación del empleado.");
        }
    }

    // Comprueba que el DTO incluya datos personales y laborales coherentes.
    private static bool RespuestaValida(RespuestaEmpleado registro)
    {
        return registro.Id != Guid.Empty &&
            Enum.IsDefined(typeof(TipoIdentificacion), registro.TipoIdentificacion) &&
            !string.IsNullOrWhiteSpace(registro.Identificacion) &&
            !string.IsNullOrWhiteSpace(registro.Nombre) &&
            !string.IsNullOrWhiteSpace(registro.PrimerApellido) &&
            !string.IsNullOrWhiteSpace(registro.SegundoApellido) &&
            registro.SalarioMensual >= 0m &&
            Enum.IsDefined(typeof(AreaTrabajo), registro.AreaTrabajo) &&
            Enum.IsDefined(typeof(Turno), registro.Turno) &&
            !string.IsNullOrWhiteSpace(registro.Puesto);
    }
}

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

// Atiende las páginas MVC de consulta y mantenimiento de proveedores.
public class ProveedoresController : ControladorApiMvcBase
{
    // Recibe la fábrica del proveedor HTTP usado por las acciones.
    public ProveedoresController(IHttpClientFactory fabricaProveedoresHttp)
        : base(fabricaProveedoresHttp)
    {
    }

    [HttpGet]
    // Consulta la colección de proveedores en la API.
    public async Task<IActionResult> Index()
    {
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                "api/proveedores", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudo cargar el listado de proveedores.");

            // Convertir solo un cuerpo JSON con la colección esperada.
            var proveedores = await LeerRespuestaAsync<RespuestaProveedor[]>(respuesta, cancelacion);
            if (proveedores is null || proveedores.Any(registro => registro is null || !RespuestaValida(registro)))
                return ServicioNoDisponible("La API respondió con datos de proveedores inesperados.");

            return View(proveedores);
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
            return ServicioNoDisponible("La API respondió con datos de proveedores inesperados.");
        }
    }

    [HttpGet]
    // Consulta un proveedor por el identificador de la ruta.
    public async Task<IActionResult> Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del proveedor no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/proveedores/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El proveedor solicitado no existe o ya fue eliminado.");
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudieron consultar los datos del proveedor.");
            // Confirmar que la respuesta corresponde al Id solicitado.
            var registro = await LeerRespuestaAsync<RespuestaProveedor>(respuesta, cancelacion);
            return registro is null || !RespuestaValida(registro) || registro.Id != id
                ? ServicioNoDisponible("La API respondió con datos de proveedor inesperados.")
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
            return ServicioNoDisponible("La API respondió con datos de proveedor inesperados.");
        }
    }

    [HttpGet]
    // Presenta el formulario vacío para registrar un proveedor.
    public IActionResult Crear() => View(new ProveedorFormulario());

    [HttpPost]
    // Valida el formulario y envía el alta a la API.
    public async Task<IActionResult> Crear([FromForm] ProveedorFormulario formulario)
    {
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.PostAsJsonAsync("api/proveedores", formulario, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.Created)
            {
                // Verificar el DTO antes de anunciar que el alta se completó.
                var registro = await LeerRespuestaAsync<RespuestaProveedor>(respuesta, cancelacion);
                if (registro is null || !RespuestaValida(registro))
                {
                    AgregarErrorMutacionNoConfirmada();
                    return View(formulario);
                }
                TempData["MensajeExito"] = "El proveedor se registró correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresApi(await LeerErrorApiAsync(respuesta, cancelacion), typeof(ProveedorFormulario));
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
    // Busca proveedores por identificación y conserva el criterio ingresado.
    public async Task<IActionResult> Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        // Conservar el criterio elegido y el texto de consulta.
        var modelo = new BusquedaViewModel<RespuestaProveedor>
        {
            Criterio = ReglasTexto.Normalizar(criterio),
            Consulta = ReglasTexto.Normalizar(consulta)
        };
        bool solicitada = Request.Query.ContainsKey(nameof(criterio)) ||
            Request.Query.ContainsKey(nameof(consulta));
        if (!solicitada)
        {
            return View(modelo);
        }
        if (modelo.Criterio is not ("Identificacion" or "RazonSocial"))
        {
            ModelState.AddModelError(nameof(modelo.Criterio), "Seleccione un criterio de búsqueda válido.");
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
            string ruta = $"api/proveedores/buscar?criterio={Uri.EscapeDataString(modelo.Criterio)}&consulta={Uri.EscapeDataString(modelo.Consulta)}";
            using var respuesta = await clienteHttp.GetAsync(ruta, HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "No se pudo consultar la API de proveedores.");
                return View(modelo);
            }
            var resultados = await LeerRespuestaAsync<RespuestaProveedor[]>(respuesta, cancelacion);
            if (resultados is null || resultados.Any(registro => registro is null || !RespuestaValida(registro)))
            {
                ModelState.AddModelError(string.Empty, "La API respondió con resultados de proveedores inesperados.");
                return View(modelo);
            }
            modelo.Resultados = resultados;
            modelo.BusquedaRealizada = true;
            return View(modelo);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo consultar la API de proveedores.");
            return View(modelo);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "La búsqueda de proveedores tardó demasiado en responder.");
            return View(modelo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            ModelState.AddModelError(string.Empty, "La API respondió con resultados de proveedores inesperados.");
            return View(modelo);
        }
    }

    [HttpGet]
    // Carga el registro y prepara sus campos editables.
    public async Task<IActionResult> Editar([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del proveedor no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/proveedores/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El proveedor solicitado no existe o ya fue eliminado.");
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los datos para editar el proveedor.");
            }
            // Mapear la respuesta de lectura al formulario editable.
            var registro = await LeerRespuestaAsync<RespuestaProveedor>(respuesta, cancelacion);
            if (registro is null || !RespuestaValida(registro) || registro.Id != id)
                return ServicioNoDisponible("La API respondió con datos de proveedor inesperados.");
            return View(new ProveedorFormulario
            {
                TipoIdentificacion = registro.TipoIdentificacion,
                Identificacion = registro.Identificacion,
                RazonSocial = registro.RazonSocial,
                NombreContacto = registro.NombreContacto,
                Telefono = registro.Telefono,
                CorreoElectronico = registro.CorreoElectronico,
                TipoSuministro = registro.TipoSuministro,
                FrecuenciaEntrega = registro.FrecuenciaEntrega
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
            return ServicioNoDisponible("La API respondió con datos de proveedor inesperados.");
        }
    }

    [HttpPost]
    // Valida los cambios y actualiza el proveedor indicado.
    public async Task<IActionResult> Editar([FromRoute] Guid id, [FromForm] ProveedorFormulario formulario)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del proveedor no contiene un identificador válido.");
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            // Enviar los campos editables sin sustituir el Id de la ruta.
            using var respuesta = await clienteHttp.PutAsJsonAsync($"api/proveedores/{id:D}", formulario, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.OK)
            {
                var registro = await LeerRespuestaAsync<RespuestaProveedor>(respuesta, cancelacion);
                if (registro is null || !RespuestaValida(registro) || registro.Id != id)
                {
                    AgregarErrorMutacionNoConfirmada();
                    return View(formulario);
                }
                TempData["MensajeExito"] = "El proveedor se actualizó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El proveedor solicitado no existe o ya fue eliminado.");
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresApi(await LeerErrorApiAsync(respuesta, cancelacion), typeof(ProveedorFormulario));
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
            return this.RegistroNoEncontrado("El enlace del proveedor no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/proveedores/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El proveedor solicitado no existe o ya fue eliminado.");
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los datos para confirmar la eliminación.");
            }
            var registro = await LeerRespuestaAsync<RespuestaProveedor>(respuesta, cancelacion);
            return registro is null || !RespuestaValida(registro) || registro.Id != id
                ? ServicioNoDisponible("La API respondió con datos de proveedor inesperados.")
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
            return ServicioNoDisponible("La API respondió con datos de proveedor inesperados.");
        }
    }

    [HttpPost]
    // Solicita la eliminación confirmada y conserva la confirmación si falla.
    public async Task<IActionResult> EliminarConfirmado([FromRoute] Guid id)
    {
        if (id == Guid.Empty || ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
            return this.RegistroNoEncontrado("El enlace del proveedor no contiene un identificador válido.");
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            // Solicitar una única baja y esperar el resultado del servicio.
            using var respuesta = await clienteHttp.DeleteAsync($"api/proveedores/{id:D}", cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NoContent)
            {
                TempData["MensajeExito"] = "El proveedor se eliminó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("El proveedor solicitado no existe o ya fue eliminado.");
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
            {
                var error = await LeerErrorApiAsync(respuesta, cancelacion);
                AgregarErroresApi(error, typeof(RespuestaProveedor));
                using var consulta = await clienteHttp.GetAsync($"api/proveedores/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
                if (consulta.StatusCode == HttpStatusCode.NotFound)
                {
                    return this.RegistroNoEncontrado("El proveedor solicitado no existe o ya fue eliminado.");
                }
                if (!consulta.IsSuccessStatusCode)
                {
                    return ServicioNoDisponible("No se pudo volver a cargar la confirmación de eliminación.");
                }
                var registro = await LeerRespuestaAsync<RespuestaProveedor>(consulta, cancelacion);
                return registro is null || !RespuestaValida(registro) || registro.Id != id
                    ? ServicioNoDisponible("La API respondió con datos de proveedor inesperados.")
                    : View("Eliminar", registro);
            }
            return ServicioNoDisponible("No se pudo confirmar la eliminación del proveedor.");
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
            return ServicioNoDisponible("No se pudo confirmar la eliminación del proveedor.");
        }
    }

    // Comprueba que el DTO incluya identificación y datos de contacto.
    private static bool RespuestaValida(RespuestaProveedor registro)
    {
        return registro.Id != Guid.Empty &&
            Enum.IsDefined(typeof(TipoIdentificacion), registro.TipoIdentificacion) &&
            !string.IsNullOrWhiteSpace(registro.Identificacion) &&
            !string.IsNullOrWhiteSpace(registro.RazonSocial) &&
            !string.IsNullOrWhiteSpace(registro.NombreContacto) &&
            !string.IsNullOrWhiteSpace(registro.Telefono) &&
            !string.IsNullOrWhiteSpace(registro.CorreoElectronico) &&
            Enum.IsDefined(typeof(TipoSuministro), registro.TipoSuministro) &&
            Enum.IsDefined(typeof(FrecuenciaEntrega), registro.FrecuenciaEntrega);
    }
}

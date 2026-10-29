using System;
using System.Collections.Generic;
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
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Carniceria.Controllers;

// Atiende las páginas MVC de consulta y mantenimiento de productos.
public class ProductosController : ControladorApiMvcBase
{
    // Recibe la fábrica del cliente HTTP usado por las acciones.
    public ProductosController(IHttpClientFactory fabricaClientesHttp)
        : base(fabricaClientesHttp)
    {
    }

    [HttpGet]
    // Consulta el listado de productos en la API.
    public async Task<IActionResult> Index()
    {
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync("api/productos", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudo cargar el inventario de productos.");
            }
            // Validar el cuerpo y la relación del proveedor antes de mostrar filas.
            var productos = await LeerRespuestaAsync<RespuestaProducto[]>(respuesta, cancelacion);
            if (productos is null || productos.Any(registro => registro is null || !RespuestaValida(registro)))
                return ServicioNoDisponible("La API respondió con datos de productos inesperados.");
            return View(productos);
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
            return ServicioNoDisponible("La API respondió con datos de productos inesperados.");
        }
    }

    [HttpGet]
    // Consulta un producto por el identificador de la ruta.
    public async Task<IActionResult> Detalles([FromRoute] Guid id)
    {
        if (!IdentificadorValido(id))
        {
            return this.RegistroNoEncontrado("El enlace del producto no contiene un identificador válido.");
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/productos/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El producto solicitado no existe o ya fue eliminado.");
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron consultar los datos del producto.");
            }
            // Confirmar el producto y su proveedor antes de renderizar detalles.
            var registro = await LeerRespuestaAsync<RespuestaProducto>(respuesta, cancelacion);
            return registro is null || !RespuestaValida(registro) || registro.Id != id
                ? ServicioNoDisponible("La API respondió con datos de producto inesperados.")
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
            return ServicioNoDisponible("La API respondió con datos de producto inesperados.");
        }
    }

    [HttpGet]
    // Carga proveedores disponibles para el formulario de alta.
    public async Task<IActionResult> Crear()
    {
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync("api/proveedores", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los proveedores del formulario.");
            }
            var proveedores = await LeerRespuestaAsync<RespuestaProveedor[]>(respuesta, cancelacion);
            if (proveedores is null || proveedores.Any(registro => registro is null || !ProveedorValido(registro)))
                return ServicioNoDisponible("La API respondió con proveedores inesperados.");
            // Presentar opciones recibidas desde API junto al formulario vacío.
            PrepararOpcionesProveedor(proveedores, null);
            return View(new ProductoFormulario());
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
            return ServicioNoDisponible("La API respondió con proveedores inesperados.");
        }
    }

    [HttpPost]
    // Valida el formulario, conserva sus valores y envía el alta a la API.
    public async Task<IActionResult> Crear([FromForm] ProductoFormulario formulario)
    {
        var cancelacion = HttpContext.RequestAborted;
        RespuestaProveedor[]? proveedores = null;
        bool solicitudDeMutacionIniciada = false;
        using var clienteHttp = CrearClienteHttpApi();
        try
        {
            using (var respuestaProveedores = await clienteHttp.GetAsync("api/proveedores", HttpCompletionOption.ResponseContentRead, cancelacion))
            {
                if (!respuestaProveedores.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(string.Empty, "No se pudieron cargar los proveedores; el producto no se envió.");
                    PrepararOpcionesProveedor(null, formulario.ProveedorId);
                    return View(formulario);
                }
                proveedores = await LeerRespuestaAsync<RespuestaProveedor[]>(respuestaProveedores, cancelacion);
            }
            if (proveedores is null || proveedores.Any(registro => registro is null || !ProveedorValido(registro)))
            {
                ModelState.AddModelError(string.Empty, "La API respondió con proveedores inesperados; el producto no se envió.");
                PrepararOpcionesProveedor(null, formulario.ProveedorId);
                return View(formulario);
            }
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            if (!ModelState.IsValid)
            {
                return View(formulario);
            }

            // Iniciar el alta después de validar el formulario y cargar proveedores.
            solicitudDeMutacionIniciada = true;
            using var respuesta = await clienteHttp.PostAsJsonAsync("api/productos", formulario, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.Created)
            {
                // Confirmar el Id y el proveedor devueltos antes de redirigir.
                var registro = await LeerRespuestaAsync<RespuestaProducto>(respuesta, cancelacion);
                if (registro is null || !RespuestaValida(registro) || registro.ProveedorId != formulario.ProveedorId)
                {
                    AgregarErrorMutacionNoConfirmada();
                    return View(formulario);
                }
                TempData["MensajeExito"] = "El producto se registró correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresApi(await LeerErrorApiAsync(respuesta, cancelacion), typeof(ProductoFormulario));
                return View(formulario);
            }
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (HttpRequestException)
        {
            if (solicitudDeMutacionIniciada)
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else ModelState.AddModelError(string.Empty, "No se pudo consultar la API; el producto no se envió.");
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            return View(formulario);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            if (solicitudDeMutacionIniciada)
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else ModelState.AddModelError(string.Empty, "La consulta de proveedores agotó el tiempo; el producto no se envió.");
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            return View(formulario);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            if (solicitudDeMutacionIniciada)
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else ModelState.AddModelError(string.Empty, "La API respondió con proveedores inesperados; el producto no se envió.");
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            return View(formulario);
        }
    }

    [HttpGet]
    // Busca productos por código exacto o nombre parcial en la API.
    public async Task<IActionResult> Buscar([FromQuery] string? criterio, [FromQuery] string? consulta)
    {
        // Conservar el criterio seleccionado y el texto de búsqueda.
        var modelo = new BusquedaViewModel<RespuestaProducto>
        {
            Criterio = ReglasTexto.Normalizar(criterio),
            Consulta = ReglasTexto.Normalizar(consulta)
        };
        bool solicitada = Request.Query.ContainsKey(nameof(criterio)) || Request.Query.ContainsKey(nameof(consulta));
        if (!solicitada)
        {
            return View(modelo);
        }
        if (modelo.Criterio is not ("Codigo" or "Nombre"))
        {
            ModelState.AddModelError(nameof(modelo.Criterio), "Seleccione un criterio de búsqueda válido.");
            return View(modelo);
        }
        if (modelo.Consulta.Length == 0)
        {
            ModelState.AddModelError(nameof(modelo.Consulta), "Ingrese un código o un nombre para buscar.");
            return View(modelo);
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            string ruta = $"api/productos/buscar?criterio={Uri.EscapeDataString(modelo.Criterio)}&consulta={Uri.EscapeDataString(modelo.Consulta)}";
            using var respuesta = await clienteHttp.GetAsync(ruta, HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "No se pudo consultar la API de productos.");
                return View(modelo);
            }
            var resultados = await LeerRespuestaAsync<RespuestaProducto[]>(respuesta, cancelacion);
            if (resultados is null || resultados.Any(registro => registro is null || !RespuestaValida(registro)))
            {
                ModelState.AddModelError(string.Empty, "La API respondió con resultados de productos inesperados.");
                return View(modelo);
            }
            modelo.Resultados = resultados;
            modelo.BusquedaRealizada = true;
            return View(modelo);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo consultar la API de productos.");
            return View(modelo);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "La búsqueda de productos tardó demasiado en responder.");
            return View(modelo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            ModelState.AddModelError(string.Empty, "La API respondió con resultados de productos inesperados.");
            return View(modelo);
        }
    }

    [HttpGet]
    // Carga el producto y los proveedores para editar sus datos.
    public async Task<IActionResult> Editar([FromRoute] Guid id)
    {
        if (!IdentificadorValido(id))
        {
            return this.RegistroNoEncontrado("El enlace del producto no contiene un identificador válido.");
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/productos/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El producto solicitado no existe o ya fue eliminado.");
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los datos para editar el producto.");
            }
            // Mapear DTO de lectura y lista de proveedores al formulario.
            var registro = await LeerRespuestaAsync<RespuestaProducto>(respuesta, cancelacion);
            if (registro is null || !RespuestaValida(registro) || registro.Id != id)
                return ServicioNoDisponible("La API respondió con datos de producto inesperados.");
            using var respuestaProveedores = await clienteHttp.GetAsync("api/proveedores", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuestaProveedores.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los proveedores del formulario.");
            }
            var proveedores = await LeerRespuestaAsync<RespuestaProveedor[]>(respuestaProveedores, cancelacion);
            if (proveedores is null || proveedores.Any(registro => registro is null || !ProveedorValido(registro)))
                return ServicioNoDisponible("La API respondió con proveedores inesperados.");
            // Marcar el proveedor principal que ya tiene el producto.
            PrepararOpcionesProveedor(proveedores, registro.ProveedorId);
            return View(new ProductoFormulario
            {
                ProveedorId = registro.ProveedorId,
                Codigo = registro.Codigo,
                Nombre = registro.Nombre,
                Categoria = registro.Categoria,
                UnidadMedida = registro.UnidadMedida,
                PrecioUnidadMedida = registro.PrecioUnidadMedida,
                ExistenciaActual = registro.ExistenciaActual,
                ExistenciaMinima = registro.ExistenciaMinima
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
            return ServicioNoDisponible("La API respondió con datos de producto inesperados.");
        }
    }

    [HttpPost]
    // Valida los cambios y conserva las opciones del proveedor seleccionado.
    public async Task<IActionResult> Editar([FromRoute] Guid id, [FromForm] ProductoFormulario formulario)
    {
        if (!IdentificadorValido(id))
        {
            return this.RegistroNoEncontrado("El enlace del producto no contiene un identificador válido.");
        }
        var cancelacion = HttpContext.RequestAborted;
        RespuestaProveedor[]? proveedores = null;
        bool solicitudDeMutacionIniciada = false;
        using var clienteHttp = CrearClienteHttpApi();
        try
        {
            using (var respuestaProveedores = await clienteHttp.GetAsync("api/proveedores", HttpCompletionOption.ResponseContentRead, cancelacion))
            {
                if (!respuestaProveedores.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(string.Empty, "No se pudieron cargar los proveedores; los cambios no se enviaron.");
                    PrepararOpcionesProveedor(null, formulario.ProveedorId);
                    return View(formulario);
                }
                proveedores = await LeerRespuestaAsync<RespuestaProveedor[]>(respuestaProveedores, cancelacion);
            }
            if (proveedores is null || proveedores.Any(registro => registro is null || !ProveedorValido(registro)))
            {
                ModelState.AddModelError(string.Empty, "La API respondió con proveedores inesperados; los cambios no se enviaron.");
                PrepararOpcionesProveedor(null, formulario.ProveedorId);
                return View(formulario);
            }
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            if (!ModelState.IsValid)
            {
                return View(formulario);
            }

            // Actualizar proveedor y valores de inventario en una sola solicitud.
            solicitudDeMutacionIniciada = true;
            using var respuesta = await clienteHttp.PutAsJsonAsync($"api/productos/{id:D}", formulario, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.OK)
            {
                var registro = await LeerRespuestaAsync<RespuestaProducto>(respuesta, cancelacion);
                if (registro is null || !RespuestaValida(registro) || registro.Id != id || registro.ProveedorId != formulario.ProveedorId)
                {
                    AgregarErrorMutacionNoConfirmada();
                    return View(formulario);
                }
                TempData["MensajeExito"] = "El producto se actualizó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El producto solicitado no existe o ya fue eliminado.");
            }
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresApi(await LeerErrorApiAsync(respuesta, cancelacion), typeof(ProductoFormulario));
                return View(formulario);
            }
            AgregarErrorMutacionNoConfirmada();
            return View(formulario);
        }
        catch (HttpRequestException)
        {
            if (solicitudDeMutacionIniciada)
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else ModelState.AddModelError(string.Empty, "No se pudo consultar la API; los cambios no se enviaron.");
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            return View(formulario);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            if (solicitudDeMutacionIniciada)
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else ModelState.AddModelError(string.Empty, "La consulta de proveedores agotó el tiempo; los cambios no se enviaron.");
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            return View(formulario);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            if (solicitudDeMutacionIniciada)
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else ModelState.AddModelError(string.Empty, "La API respondió con proveedores inesperados; los cambios no se enviaron.");
            PrepararOpcionesProveedor(proveedores, formulario.ProveedorId);
            return View(formulario);
        }
    }

    [HttpGet]
    // Carga los datos antes de presentar la confirmación de eliminación.
    public async Task<IActionResult> Eliminar([FromRoute] Guid id)
    {
        if (!IdentificadorValido(id))
        {
            return this.RegistroNoEncontrado("El enlace del producto no contiene un identificador válido.");
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync($"api/productos/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El producto solicitado no existe o ya fue eliminado.");
            }
            if (!respuesta.IsSuccessStatusCode)
            {
                return ServicioNoDisponible("No se pudieron cargar los datos para confirmar la eliminación.");
            }
            var registro = await LeerRespuestaAsync<RespuestaProducto>(respuesta, cancelacion);
            return registro is null || !RespuestaValida(registro) || registro.Id != id
                ? ServicioNoDisponible("La API respondió con datos de producto inesperados.")
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
            return ServicioNoDisponible("La API respondió con datos de producto inesperados.");
        }
    }

    [HttpPost]
    // Solicita la baja y mantiene la confirmación ante un conflicto de relación.
    public async Task<IActionResult> EliminarConfirmado([FromRoute] Guid id)
    {
        if (!IdentificadorValido(id))
        {
            return this.RegistroNoEncontrado("El enlace del producto no contiene un identificador válido.");
        }
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.DeleteAsync($"api/productos/{id:D}", cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NoContent)
            {
                TempData["MensajeExito"] = "El producto se eliminó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("El producto solicitado no existe o ya fue eliminado.");
            }
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
            {
                var error = await LeerErrorApiAsync(respuesta, cancelacion);
                AgregarErroresApi(error, typeof(RespuestaProducto));
                // Recuperar el snapshot actual para conservar la confirmación visible.
                using var consulta = await clienteHttp.GetAsync($"api/productos/{id:D}", HttpCompletionOption.ResponseContentRead, cancelacion);
                if (consulta.StatusCode == HttpStatusCode.NotFound)
                {
                    return this.RegistroNoEncontrado("El producto solicitado no existe o ya fue eliminado.");
                }
                if (!consulta.IsSuccessStatusCode)
                {
                    return ServicioNoDisponible("No se pudo volver a cargar la confirmación de eliminación.");
                }
                var registro = await LeerRespuestaAsync<RespuestaProducto>(consulta, cancelacion);
                return registro is null || !RespuestaValida(registro) || registro.Id != id
                    ? ServicioNoDisponible("La API respondió con datos de producto inesperados.")
                    : View("Eliminar", registro);
            }
            return ServicioNoDisponible("No se pudo confirmar la eliminación del producto.");
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
            return ServicioNoDisponible("No se pudo confirmar la eliminación del producto.");
        }
    }

    // Convierte proveedores recibidos en opciones y conserva el Id seleccionado.
    private void PrepararOpcionesProveedor(IReadOnlyList<RespuestaProveedor>? proveedores, Guid? seleccionado)
    {
        var opciones = (proveedores ?? Array.Empty<RespuestaProveedor>())
            .Select(proveedor => new SelectListItem
            {
                Value = proveedor.Id.ToString("D"),
                Text = $"{proveedor.RazonSocial} ({proveedor.Identificacion})",
                Selected = seleccionado == proveedor.Id
            })
            .ToList();
        if (seleccionado.HasValue && opciones.All(opcion => opcion.Value != seleccionado.Value.ToString("D")))
            opciones.Insert(0, new SelectListItem
            {
                Value = seleccionado.Value.ToString("D"),
                Text = "Proveedor seleccionado no disponible",
                Selected = true
            });
        ViewBag.Proveedores = opciones;
    }

    // Rechaza identificadores vacíos o rutas que no pudieron enlazarse.
    private bool IdentificadorValido(Guid id)
    {
        return id != Guid.Empty &&
            ModelState.GetFieldValidationState(nameof(id)) != ModelValidationState.Invalid;
    }

    // Comprueba que el DTO incluya proveedor y datos de inventario utilizables.
    private static bool RespuestaValida(RespuestaProducto registro)
    {
        return registro.Id != Guid.Empty &&
            registro.ProveedorId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(registro.ProveedorRazonSocial) &&
            !string.IsNullOrWhiteSpace(registro.ProveedorIdentificacion) &&
            !string.IsNullOrWhiteSpace(registro.Codigo) &&
            !string.IsNullOrWhiteSpace(registro.Nombre) &&
            Enum.IsDefined(typeof(CategoriaProducto), registro.Categoria) &&
            Enum.IsDefined(typeof(UnidadMedida), registro.UnidadMedida) &&
            registro.PrecioUnidadMedida > 0m &&
            registro.ExistenciaActual >= 0m &&
            registro.ExistenciaMinima >= 0m;
    }

    // Comprueba que las opciones provengan de proveedores con datos completos.
    private static bool ProveedorValido(RespuestaProveedor proveedor)
    {
        return proveedor.Id != Guid.Empty &&
            !string.IsNullOrWhiteSpace(proveedor.Identificacion) &&
            !string.IsNullOrWhiteSpace(proveedor.RazonSocial);
    }
}

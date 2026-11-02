using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Carniceria.Modelos.Consultas;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

// Consulta facturas y confirma sus transiciones disponibles.
public sealed class FacturasController : ControladorApiMvcBase
{
    // Recibe la fabrica del cliente HTTP usado por las acciones.
    public FacturasController(IHttpClientFactory fabricaClientesHttp)
        : base(fabricaClientesHttp)
    {
    }

    [HttpGet]
    // Consulta la coleccion vigente de facturas.
    public async Task<IActionResult> Index()
    {
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                "api/facturas", HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudo cargar el listado de facturas.");

            var facturas = await LeerRespuestaAsync<RespuestaFactura[]>(respuesta, cancelacion);
            if (facturas is null || facturas.Any(factura => !RespuestaValida(factura)))
                return ServicioNoDisponible("La API respondió con datos de facturas inesperados.");

            return View(facturas);
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
            return ServicioNoDisponible("La API respondió con datos de facturas inesperados.");
        }
    }

    [HttpGet]
    // Consulta los datos de una factura por numero.
    public async Task<IActionResult> Detalles([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudieron consultar los datos de la factura.");

            var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
            if (factura is null || !RespuestaValida(factura) || !NumeroCoincide(factura, numero))
                return ServicioNoDisponible("La API respondió con datos de factura inesperados.");

            return View(factura);
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
            return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
        }
    }

    [HttpGet]
    // Busca facturas por numero o identificacion del cliente.
    public async Task<IActionResult> Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        var modelo = new BusquedaViewModel<RespuestaFactura>
        {
            Criterio = criterio?.Trim() ?? string.Empty,
            Consulta = consulta?.Trim() ?? string.Empty
        };

        bool enviada = Request.Query.ContainsKey(nameof(criterio)) ||
            Request.Query.ContainsKey(nameof(consulta));
        if (!enviada)
            return View(modelo);

        if (!CriterioValido(modelo.Criterio))
            ModelState.AddModelError(nameof(modelo.Criterio), "Seleccione un criterio válido.");
        if (modelo.Consulta.Length == 0)
            ModelState.AddModelError(nameof(modelo.Consulta), "Ingrese un texto para buscar.");
        if (!ModelState.IsValid)
            return View(modelo);

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            string nombreCriterio = string.Equals(
                modelo.Criterio, "Numero", StringComparison.OrdinalIgnoreCase)
                ? "Numero"
                : "IdentificacionCliente";
            string ruta = "api/facturas/buscar?criterio=" +
                Uri.EscapeDataString(nombreCriterio) + "&consulta=" +
                Uri.EscapeDataString(modelo.Consulta);
            using var respuesta = await clienteHttp.GetAsync(
                ruta, HttpCompletionOption.ResponseContentRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "No se pudo consultar la API de facturas.");
                return View(modelo);
            }

            var resultados = await LeerRespuestaAsync<RespuestaFactura[]>(respuesta, cancelacion);
            if (resultados is null || resultados.Any(factura => !RespuestaValida(factura)))
            {
                ModelState.AddModelError(string.Empty, "La API respondió con resultados inesperados.");
                return View(modelo);
            }

            modelo.Resultados = resultados;
            modelo.BusquedaRealizada = true;
            return View(modelo);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo consultar la API de facturas.");
            return View(modelo);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "La consulta de facturas tardó demasiado.");
            return View(modelo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            ModelState.AddModelError(string.Empty, "La API respondió con resultados inesperados.");
            return View(modelo);
        }
    }

    [HttpGet]
    // Muestra la confirmacion de pago solo para una factura Pendiente.
    public async Task<IActionResult> Pagar([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudieron consultar los datos para confirmar el pago.");

            var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
            if (factura is null || !RespuestaValida(factura) || !NumeroCoincide(factura, numero))
                return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
            if (factura.Estado != EstadoFactura.Pendiente)
                ModelState.AddModelError(string.Empty, MensajeEstadoNoPermitido(factura.Estado));
            return View(factura);
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
            return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
        }
    }

    [HttpPost]
    // Solicita el pago una vez y consulta el estado si se pierde la respuesta.
    public async Task<IActionResult> PagarConfirmado([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.PostAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}/pagar", content: null, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.OK)
            {
                var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
                if (factura is not null && RespuestaValida(factura) &&
                    NumeroCoincide(factura, numero) && factura.Estado == EstadoFactura.Pagada)
                {
                    TempData["MensajeExito"] = $"El pago de la factura {factura.Numero} se confirmó correctamente.";
                    return RedirectToAction(nameof(Detalles), new { numero = factura.Numero });
                }
            }
            else if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            }
            else if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErrorApiAlResumen(await LeerErrorApiAsync(respuesta, cancelacion));
                using var consulta = await clienteHttp.GetAsync(
                    $"api/facturas/{Uri.EscapeDataString(numero)}",
                    HttpCompletionOption.ResponseContentRead,
                    cancelacion);
                if (consulta.StatusCode == HttpStatusCode.NotFound)
                    return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
                if (!consulta.IsSuccessStatusCode)
                    return ServicioNoDisponible("No se pudo volver a cargar la confirmación de pago.");
                var actual = await LeerRespuestaAsync<RespuestaFactura>(consulta, cancelacion);
                if (actual is null || !RespuestaValida(actual) || !NumeroCoincide(actual, numero))
                    return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
                return View("Pagar", actual);
            }

        }
        catch (HttpRequestException)
        {
            // La consulta posterior determina si el pago se aplico.
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            // La consulta posterior determina si el pago se aplico.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            // La consulta posterior determina si el pago se aplico.
        }

        // Consultar el estado antes de informar un resultado incierto.
        using var clienteEstado = CrearClienteHttpApi();
        try
        {
            using var estado = await clienteEstado.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (estado.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (!estado.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudo confirmar el estado del pago. Consulte la factura antes de volver a intentarlo.");

            var actual = await LeerRespuestaAsync<RespuestaFactura>(estado, cancelacion);
            if (actual is null || !RespuestaValida(actual) || !NumeroCoincide(actual, numero))
                return ServicioNoDisponible("No se pudo confirmar el estado del pago. Consulte la factura antes de volver a intentarlo.");
            if (actual.Estado == EstadoFactura.Pagada)
            {
                TempData["MensajeExito"] = $"Se verificó que el pago de la factura {actual.Numero} fue aplicado.";
                return RedirectToAction(nameof(Detalles), new { numero = actual.Numero });
            }
            return ServicioNoDisponible("No se pudo confirmar si el pago fue aplicado. Consulte el estado antes de volver a intentarlo.");
        }
        catch (HttpRequestException)
        {
            return ServicioNoDisponible("No se pudo confirmar si el pago fue aplicado.");
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return ServicioNoDisponible("No se pudo confirmar si el pago fue aplicado.");
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("No se pudo confirmar si el pago fue aplicado.");
        }
    }

    [HttpGet]
    // Muestra la confirmacion de anulacion solo para una factura Pagada.
    public async Task<IActionResult> Anular([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudieron consultar los datos para confirmar la anulación.");

            var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
            if (factura is null || !RespuestaValida(factura) || !NumeroCoincide(factura, numero))
                return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
            if (factura.Estado != EstadoFactura.Pagada)
                ModelState.AddModelError(string.Empty, MensajeEstadoNoPermitido(factura.Estado));
            return View(factura);
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
            return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
        }
    }

    [HttpPost]
    // Solicita una anulacion y valida la respuesta del servicio.
    public async Task<IActionResult> AnularConfirmado([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.PostAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}/anular", content: null, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.OK)
            {
                var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
                if (factura is not null && RespuestaValida(factura) &&
                    NumeroCoincide(factura, numero) && factura.Estado == EstadoFactura.Anulada)
                {
                    TempData["MensajeExito"] = $"La factura {factura.Numero} se anuló correctamente.";
                    return RedirectToAction(nameof(Detalles), new { numero = factura.Numero });
                }
            }
            else if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            }
            else if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErrorApiAlResumen(await LeerErrorApiAsync(respuesta, cancelacion));
                using var consulta = await clienteHttp.GetAsync(
                    $"api/facturas/{Uri.EscapeDataString(numero)}",
                    HttpCompletionOption.ResponseContentRead,
                    cancelacion);
                if (consulta.StatusCode == HttpStatusCode.NotFound)
                    return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
                if (!consulta.IsSuccessStatusCode)
                    return ServicioNoDisponible("No se pudo volver a cargar la confirmación de anulación.");
                var actual = await LeerRespuestaAsync<RespuestaFactura>(consulta, cancelacion);
                if (actual is null || !RespuestaValida(actual) || !NumeroCoincide(actual, numero))
                    return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
                return View("Anular", actual);
            }

        }
        catch (HttpRequestException)
        {
            // La consulta posterior determina si la anulacion se aplico.
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            // La consulta posterior determina si la anulacion se aplico.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            // La consulta posterior determina si la anulacion se aplico.
        }

        // Consultar el estado antes de informar un resultado incierto.
        using var clienteEstado = CrearClienteHttpApi();
        try
        {
            using var estado = await clienteEstado.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (estado.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (!estado.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudo confirmar el estado de la anulación.");

            var actual = await LeerRespuestaAsync<RespuestaFactura>(estado, cancelacion);
            if (actual is null || !RespuestaValida(actual) || !NumeroCoincide(actual, numero))
                return ServicioNoDisponible("No se pudo confirmar el estado de la anulación.");
            if (actual.Estado == EstadoFactura.Anulada)
            {
                TempData["MensajeExito"] = $"Se verificó que la factura {actual.Numero} fue anulada.";
                return RedirectToAction(nameof(Detalles), new { numero = actual.Numero });
            }
            return ServicioNoDisponible("No se pudo confirmar si la anulación fue aplicada. Consulte el estado antes de volver a intentarlo.");
        }
        catch (HttpRequestException)
        {
            return ServicioNoDisponible("No se pudo confirmar si la anulación fue aplicada.");
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return ServicioNoDisponible("No se pudo confirmar si la anulación fue aplicada.");
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("No se pudo confirmar si la anulación fue aplicada.");
        }
    }

    [HttpGet]
    // Muestra la confirmacion de baja solo para una factura Pendiente.
    public async Task<IActionResult> Eliminar([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (!respuesta.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudieron consultar los datos para confirmar la baja.");

            var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
            if (factura is null || !RespuestaValida(factura) || !NumeroCoincide(factura, numero))
                return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
            if (factura.Estado != EstadoFactura.Pendiente)
                ModelState.AddModelError(string.Empty, MensajeEstadoNoPermitido(factura.Estado));
            return View(factura);
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
            return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
        }
    }

    [HttpPost]
    // Solicita la baja y confirma el estado si la respuesta se pierde.
    public async Task<IActionResult> EliminarConfirmado([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            using var respuesta = await clienteHttp.DeleteAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}", cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.NoContent)
            {
                TempData["MensajeExito"] = $"La factura {numero} se eliminó correctamente.";
                return RedirectToAction(nameof(Index));
            }
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErrorApiAlResumen(await LeerErrorApiAsync(respuesta, cancelacion));
                using var consulta = await clienteHttp.GetAsync(
                    $"api/facturas/{Uri.EscapeDataString(numero)}",
                    HttpCompletionOption.ResponseContentRead,
                    cancelacion);
                if (consulta.StatusCode == HttpStatusCode.NotFound)
                    return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
                if (!consulta.IsSuccessStatusCode)
                    return ServicioNoDisponible("No se pudo volver a cargar la confirmación de baja.");
                var actual = await LeerRespuestaAsync<RespuestaFactura>(consulta, cancelacion);
                if (actual is null || !RespuestaValida(actual) || !NumeroCoincide(actual, numero))
                    return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
                return View("Eliminar", actual);
            }

        }
        catch (HttpRequestException)
        {
            // La consulta posterior determina si la baja se aplico.
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            // La consulta posterior determina si la baja se aplico.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            // La consulta posterior determina si la baja se aplico.
        }

        // Consultar la ausencia del registro antes de confirmar la baja.
        using var clienteEstado = CrearClienteHttpApi();
        try
        {
            using var estado = await clienteEstado.GetAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                HttpCompletionOption.ResponseContentRead,
                cancelacion);
            if (estado.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["MensajeExito"] = $"Se verificó que la factura {numero} fue eliminada.";
                return RedirectToAction(nameof(Index));
            }
            if (!estado.IsSuccessStatusCode)
                return ServicioNoDisponible("No se pudo confirmar el resultado de la baja.");

            var actual = await LeerRespuestaAsync<RespuestaFactura>(estado, cancelacion);
            if (actual is null || !RespuestaValida(actual) || !NumeroCoincide(actual, numero))
                return ServicioNoDisponible("No se pudo confirmar el resultado de la baja.");
            return ServicioNoDisponible("No se pudo confirmar el resultado de la baja. Consulte la factura antes de volver a intentarlo.");
        }
        catch (HttpRequestException)
        {
            return ServicioNoDisponible("No se pudo confirmar el resultado de la baja.");
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return ServicioNoDisponible("No se pudo confirmar el resultado de la baja.");
        }
        catch (System.Text.Json.JsonException)
        {
            return ServicioNoDisponible("No se pudo confirmar el resultado de la baja.");
        }
    }

    // Agrega los mensajes de error de API al resumen de la confirmacion.
    private void AgregarErrorApiAlResumen(ErrorApi error)
    {
        bool agregado = false;
        if (!string.IsNullOrWhiteSpace(error.Mensaje))
        {
            ModelState.AddModelError(string.Empty, error.Mensaje);
            agregado = true;
        }

        foreach (string mensaje in (error.ErroresPorCampo ?? new())
            .SelectMany(par => par.Value ?? Array.Empty<string>())
            .Where(texto => !string.IsNullOrWhiteSpace(texto)))
        {
            ModelState.AddModelError(string.Empty, mensaje);
            agregado = true;
        }

        if (!agregado)
            ModelState.AddModelError(string.Empty, "La API rechazó la operación.");
    }

    // Valida los datos que se muestran sin recalcular importes en MVC.
    private static bool RespuestaValida(RespuestaFactura? factura)
    {
        return factura is not null &&
            factura.Id != Guid.Empty &&
            !string.IsNullOrWhiteSpace(factura.Numero) &&
            factura.FechaEmision != default &&
            factura.ClienteId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(factura.ClienteIdentificacion) &&
            !string.IsNullOrWhiteSpace(factura.ClienteNombreCompleto) &&
            factura.EmpleadoId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(factura.EmpleadoIdentificacion) &&
            !string.IsNullOrWhiteSpace(factura.EmpleadoNombreCompleto) &&
            Enum.IsDefined(typeof(MetodoPago), factura.MetodoPago) &&
            Enum.IsDefined(typeof(EstadoFactura), factura.Estado) &&
            factura.PorcentajeDescuento >= 0 &&
            factura.Subtotal >= 0 && factura.MontoDescuento >= 0 &&
            factura.BaseGravable >= 0 && factura.MontoIva >= 0 && factura.Total >= 0 &&
            factura.Detalles is { Count: > 0 } &&
            factura.Detalles.All(detalle => detalle is not null &&
                detalle.Id != Guid.Empty && detalle.ProductoId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(detalle.ProductoCodigo) &&
                !string.IsNullOrWhiteSpace(detalle.ProductoNombre) &&
                Enum.IsDefined(typeof(UnidadMedida), detalle.UnidadMedida) &&
                detalle.Cantidad > 0 && detalle.PrecioUnitario >= 0 && detalle.SubtotalLinea >= 0);
    }

    // Compara el numero de ruta sin distinguir mayusculas.
    private static bool NumeroCoincide(RespuestaFactura factura, string numero) =>
        string.Equals(factura.Numero, numero, StringComparison.OrdinalIgnoreCase);

    // Limita la busqueda a los dos criterios definidos por la API.
    private static bool CriterioValido(string criterio) =>
        string.Equals(criterio, "Numero", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(criterio, "IdentificacionCliente", StringComparison.OrdinalIgnoreCase);

    // Describe el estado que impide ejecutar la transicion solicitada.
    private static string MensajeEstadoNoPermitido(EstadoFactura estado) => estado switch
    {
        EstadoFactura.Pendiente => "La operación no está disponible para una factura Pendiente.",
        EstadoFactura.Pagada => "La operación no está disponible para una factura Pagada.",
        EstadoFactura.Anulada => "La operación no está disponible para una factura Anulada.",
        _ => "La operación no está disponible para el estado actual."
    };
}

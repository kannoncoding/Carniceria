using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Carniceria.Web.Helpers;

// Comparte la lectura de contratos API y el tratamiento de sus errores.
public abstract class ControladorApiMvcBase : Controller
{
    // Fábrica registrada para las consultas HTTP a la API.
    private readonly IHttpClientFactory _fabricaClientesHttp;

    // Recibe la fábrica de clientes HTTP configurada por la aplicación.
    protected ControladorApiMvcBase(IHttpClientFactory fabricaClientesHttp)
    {
        _fabricaClientesHttp = fabricaClientesHttp;
    }

    // Crea el cliente con la dirección base y el tiempo límite registrados.
    protected HttpClient CrearClienteHttpApi()
    {
        return _fabricaClientesHttp.CreateClient("ApiCarniceria");
    }

    // Lee el cuerpo JSON de una respuesta completada.
    protected static async Task<T?> LeerRespuestaAsync<T>(
        HttpResponseMessage respuesta,
        CancellationToken cancelacion)
    {
        return await respuesta.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancelacion);
    }

    // Lee un error uniforme o prepara un mensaje según el código HTTP.
    protected static async Task<ErrorApi> LeerErrorApiAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancelacion)
    {
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<ErrorApi>(
                cancellationToken: cancelacion);

            if (error is not null &&
                (!string.IsNullOrWhiteSpace(error.Mensaje) ||
                 error.ErroresPorCampo is { Count: > 0 }))
            {
                return error;
            }
        }
        catch (JsonException)
        {
            // El cuerpo no sigue el contrato uniforme de errores.
        }

        return new ErrorApi
        {
            Codigo = $"Http{(int)respuesta.StatusCode}",
            Mensaje = "La API rechazó la operación.",
            ErroresPorCampo = new Dictionary<string, string[]>()
        };
    }

    // Asocia los errores API a campos conocidos o al resumen general.
    protected void AgregarErroresApi(ErrorApi error, Type tipoFormulario)
    {
        const string mensajeGeneral = "La API rechazó la operación.";
        int erroresIniciales = ModelState.ErrorCount;
        if (error.ErroresPorCampo is not { Count: > 0 })
        {
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(error.Mensaje)
                    ? mensajeGeneral
                    : error.Mensaje);
            return;
        }

        // Limitar las claves del servidor a propiedades del formulario actual.
        var propiedades = tipoFormulario
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(
                propiedad => propiedad.Name,
                propiedad => propiedad.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var (clave, mensajes) in error.ErroresPorCampo ?? new())
        {
            string nombreCampo = NormalizarNombreCampo(clave);
            bool campoReconocido = propiedades.TryGetValue(
                nombreCampo,
                out string? nombrePropiedad);

            // Ignorar mensajes vacíos y llevar claves desconocidas al resumen.
            string[] textos = (mensajes ?? Array.Empty<string>())
                .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
                .ToArray();

            if (!campoReconocido)
            {
                // Mostrar todos los mensajes de campos ajenos al formulario.
                if (textos.Length == 0)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        string.IsNullOrWhiteSpace(error.Mensaje)
                            ? mensajeGeneral
                            : error.Mensaje);
                    continue;
                }

                foreach (string texto in textos)
                {
                    ModelState.AddModelError(string.Empty, texto);
                }
                continue;
            }

            foreach (string texto in textos)
            {
                ModelState.AddModelError(nombrePropiedad!, texto);
            }
        }

        // Evitar ocultar errores si el servidor entregó campos sin mensajes.
        if (ModelState.ErrorCount == erroresIniciales)
        {
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(error.Mensaje)
                    ? mensajeGeneral
                    : error.Mensaje);
        }
    }

    // Devuelve el nombre del campo desde claves de validación JSON.
    private static string NormalizarNombreCampo(string clave)
    {
        string limpia = clave.Trim();
        if (limpia.StartsWith("$.", StringComparison.Ordinal))
            return limpia[2..];
        if (limpia.StartsWith("solicitud.", StringComparison.OrdinalIgnoreCase))
            return limpia["solicitud.".Length..];
        return limpia;
    }

    // Presenta una falla de API sin confundirla con una lista vacía.
    protected IActionResult ServicioNoDisponible(string mensaje)
    {
        var vista = View(
            "~/Views/Shared/Error.cshtml",
            new ErrorViewModel
            {
                Titulo = "Servicio de datos no disponible",
                Mensaje = mensaje,
                ControladorRegreso = "Home",
                TextoRegreso = "Volver al inicio"
            });
        vista.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return vista;
    }

    // Distingue una espera agotada de la cancelación del navegador.
    protected IActionResult ErrorDeComunicacion(
        CancellationToken cancelacion,
        bool operacionMutante)
    {
        if (cancelacion.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancelacion);
        }

        string mensaje = operacionMutante
            ? "No se pudo confirmar el resultado de la operación. " +
              "Consulte el listado antes de volver a enviarla."
            : "No se pudo consultar el servicio de datos. Inténtelo más tarde.";

        return ServicioNoDisponible(mensaje);
    }

    // Conserva el formulario cuando no se pudo confirmar una mutación.
    protected void AgregarErrorMutacionNoConfirmada()
    {
        ModelState.AddModelError(
            string.Empty,
            "No se pudo confirmar el resultado de la operación. " +
            "Consulte el listado antes de volver a enviarla.");
    }
}

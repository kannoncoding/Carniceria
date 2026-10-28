using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Carniceria.Models;
using Carniceria.Web.Helpers;
using Carniceria.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _fabricaClientesHttp;

    public HomeController(IHttpClientFactory fabricaClientesHttp)
    {
        _fabricaClientesHttp = fabricaClientesHttp;
    }

    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var modelo = new ModeloEstadoServicio();
        var cliente = _fabricaClientesHttp.CreateClient("ApiCarniceria");
        using var limiteEspera = CancellationTokenSource.CreateLinkedTokenSource(
            cancelacion);
        limiteEspera.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            using var respuesta = await cliente.GetAsync(
                "api/estado",
                HttpCompletionOption.ResponseContentRead,
                limiteEspera.Token);

            if (!respuesta.IsSuccessStatusCode)
            {
                modelo.Mensaje = "El servicio respondió con un error.";
                return View(modelo);
            }

            var contenido = await respuesta.Content.ReadAsStringAsync(
                limiteEspera.Token);
            using var documento = JsonDocument.Parse(contenido);

            if (documento.RootElement.ValueKind == JsonValueKind.Object &&
                documento.RootElement.TryGetProperty("estado", out var estado) &&
                estado.ValueKind == JsonValueKind.String &&
                estado.GetString() == "Disponible")
            {
                modelo.Disponible = true;
                modelo.Mensaje = "El servicio de datos está disponible.";
            }
            else
            {
                modelo.Mensaje = "El servicio respondió con datos inesperados.";
            }
        }
        catch (HttpRequestException)
        {
            modelo.Mensaje = "El servicio de datos no está disponible.";
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            modelo.Mensaje = "El servicio de datos tardó demasiado en responder.";
        }
        catch (JsonException)
        {
            modelo.Mensaje = "El servicio respondió con datos inesperados.";
        }
        catch (IOException)
        {
            modelo.Mensaje = "El servicio respondió con datos inesperados.";
        }

        return View(modelo);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult EstadoHttp([FromRoute] int id)
    {
        if (id == StatusCodes.Status404NotFound)
        {
            return this.RegistroNoEncontrado(
                "La página o el registro solicitado no existe " +
                "o ya no está disponible.");
        }

        if (id < 400 || id > 599)
        {
            id = StatusCodes.Status400BadRequest;
        }

        var vista = View("Error", CrearModeloError());

        vista.StatusCode = id;

        return vista;
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        var vista = View("Error", CrearModeloError());

        vista.StatusCode = StatusCodes.Status500InternalServerError;

        return vista;
    }

    private ErrorViewModel CrearModeloError()
    {
        return new ErrorViewModel
        {
            RequestId =
                Activity.Current?.Id ??
                HttpContext.TraceIdentifier
        };
    }
}

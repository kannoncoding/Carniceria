using System.Diagnostics;
using Carniceria.Models;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
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
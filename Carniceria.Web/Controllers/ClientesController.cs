using System;
using Carniceria.Modelos.Formularios;
using Carniceria.Web.Data;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

public class ClientesController : Controller
{
    private readonly IAlmacenCarniceria _almacen;

    public ClientesController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var clientes = _almacen.ListarClientes();

        return View(clientes);
    }

    [HttpGet]
    public IActionResult Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty || !ModelState.IsValid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del cliente no contiene un identificador válido.");
        }

        var cliente = _almacen.ObtenerCliente(id);

        if (cliente is null)
        {
            return this.RegistroNoEncontrado(
                "El cliente solicitado no existe o ya fue eliminado.");
        }

        return View(cliente);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        var formulario = new ClienteFormulario();

        return View(formulario);
    }

    [HttpPost]
    public IActionResult Crear([FromForm] ClienteFormulario formulario)
    {
        // Conservar el formulario y sus errores cuando los datos sean inválidos.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.CrearCliente(formulario);

        // Incorporar errores del almacén, como una identificación duplicada.
        if (!resultado.EsExitoso)
        {
            ModelState.AgregarErrores(resultado);

            return View(formulario);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        // Redirigir después de guardar para evitar repetir el envío al actualizar.
        return RedirectToAction(nameof(Index));
    }
}
using System;
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
}
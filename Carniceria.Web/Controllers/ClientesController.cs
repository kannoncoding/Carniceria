using Carniceria.Web.Data;
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
}
using Carniceria.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

public class ProductosController : Controller
{
    private readonly IAlmacenCarniceria _almacen;

    public ProductosController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var productos = _almacen.ListarProductos();

        return View(productos);
    }
}
using Carniceria.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

public class ProveedoresController : Controller
{
    private readonly IAlmacenCarniceria _almacen;

    public ProveedoresController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var proveedores = _almacen.ListarProveedores();

        return View(proveedores);
    }
}
using Carniceria.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Controllers;

public class EmpleadosController : Controller
{
    private readonly IAlmacenCarniceria _almacen;

    public EmpleadosController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var empleados = _almacen.ListarEmpleados();

        return View(empleados);
    }
}
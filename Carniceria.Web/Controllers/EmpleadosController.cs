using System;
using Carniceria.Web.Data;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    [HttpGet]
    public IActionResult Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del empleado no contiene un identificador válido.");
        }

        var empleado = _almacen.ObtenerEmpleado(id);

        if (empleado is null)
        {
            return this.RegistroNoEncontrado(
                "El empleado solicitado no existe o ya fue eliminado.");
        }

        return View(empleado);
    }
}
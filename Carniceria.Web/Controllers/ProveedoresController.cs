using System;
using Carniceria.Web.Data;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    [HttpGet]
    public IActionResult Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del proveedor no contiene un identificador válido.");
        }

        var proveedor = _almacen.ObtenerProveedor(id);

        if (proveedor is null)
        {
            return this.RegistroNoEncontrado(
                "El proveedor solicitado no existe o ya fue eliminado.");
        }

        return View(proveedor);
    }
}
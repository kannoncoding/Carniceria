using System;
using Carniceria.Web.Data;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    [HttpGet]
    public IActionResult Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del producto no contiene un identificador válido.");
        }

        var producto = _almacen.ObtenerProducto(id);

        if (producto is null)
        {
            return this.RegistroNoEncontrado(
                "El producto solicitado no existe o ya fue eliminado.");
        }

        return View(producto);
    }
}
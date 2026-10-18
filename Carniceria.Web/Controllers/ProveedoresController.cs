using System;
using Carniceria.Modelos.Formularios;
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

    [HttpGet]
    public IActionResult Crear()
    {
        var formulario = new ProveedorFormulario();

        return View(formulario);
    }

    [HttpPost]
    public IActionResult Crear([FromForm] ProveedorFormulario formulario)
    {
        // Conservar los valores enviados y mostrar los errores del formulario.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.CrearProveedor(formulario);

        // Incorporar errores del almacén, como una identificación duplicada.
        if (!resultado.EsExitoso)
        {
            ModelState.AgregarErrores(resultado);

            return View(formulario);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        // Redirigir después de guardar para evitar repetir el envío.
        return RedirectToAction(nameof(Index));
    }
}
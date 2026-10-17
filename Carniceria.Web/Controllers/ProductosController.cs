using System;
using Carniceria.Modelos.Consultas;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Validacion;
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

    [HttpGet]
    public IActionResult Crear()
    {
        var formulario = new ProductoFormulario();

        return View(formulario);
    }

    [HttpPost]
    public IActionResult Crear([FromForm] ProductoFormulario formulario)
    {
        // Conservar los valores enviados y mostrar los errores del formulario.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.CrearProducto(formulario);

        // Incorporar errores del almacén, como un código duplicado.
        if (!resultado.EsExitoso)
        {
            ModelState.AgregarErrores(resultado);

            return View(formulario);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        // Redirigir después de guardar para evitar repetir el envío.
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        var modelo = new BusquedaViewModel<Producto>
        {
            Criterio = ReglasTexto.Normalizar(criterio),
            Consulta = ReglasTexto.Normalizar(consulta)
        };

        // Al abrir la página sin parámetros, mostrar las instrucciones.
        bool busquedaSolicitada =
            Request.Query.ContainsKey(nameof(criterio)) ||
            Request.Query.ContainsKey(nameof(consulta));

        if (!busquedaSolicitada)
        {
            return View(modelo);
        }

        // Validar el criterio también en el servidor.
        if (modelo.Criterio.Length == 0)
        {
            ModelState.AddModelError(
                nameof(modelo.Criterio),
                "Seleccione un criterio de búsqueda.");
        }
        else if (modelo.Criterio != "Codigo" &&
                 modelo.Criterio != "Nombre")
        {
            ModelState.AddModelError(
                nameof(modelo.Criterio),
                "Seleccione un criterio de búsqueda válido.");
        }

        if (modelo.Consulta.Length == 0)
        {
            ModelState.AddModelError(
                nameof(modelo.Consulta),
                "Ingrese un código o un nombre para buscar.");
        }

        // Conservar el criterio y el texto cuando haya errores.
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // Código: coincidencia exacta. Nombre: coincidencias parciales.
        modelo.Resultados = modelo.Criterio == "Codigo"
            ? _almacen.BuscarProductosPorCodigo(modelo.Consulta)
            : _almacen.BuscarProductosPorNombre(modelo.Consulta);

        modelo.BusquedaRealizada = true;

        return View(modelo);
    }
}
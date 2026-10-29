using System;
using Carniceria.Modelos.Consultas;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Validacion;
using Carniceria.Web.Data;
using Carniceria.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Carniceria.Controllers;

// Atiende las páginas MVC de consulta y mantenimiento de productos.
public class ProductosController : Controller
{
    // Acceso al almacén en memoria usado temporalmente por Web.
    private readonly IAlmacenCarniceria _almacen;

    // Recibe el almacén registrado para las acciones del controlador.
    public ProductosController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    // Muestra el listado de productos.
    public IActionResult Index()
    {
        var productos = _almacen.ListarProductos();

        return View(productos);
    }

    [HttpGet]
    // Muestra la ficha del producto indicado.
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
    // Presenta el formulario vacío de alta.
    public IActionResult Crear()
    {
        var formulario = new ProductoFormulario();

        return View(formulario);
    }

    [HttpPost]
    // Valida y guarda el producto enviado desde el formulario.
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
    // Busca productos por código exacto o nombre parcial.
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

    [HttpGet]
    // Carga los datos del producto para editarlo.
    public IActionResult Editar([FromRoute] Guid id)
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

        // Copiar todos los campos editables al modelo del formulario.
        var formulario = new ProductoFormulario
        {
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Categoria = producto.Categoria,
            UnidadMedida = producto.UnidadMedida,
            PrecioUnidadMedida = producto.PrecioUnidadMedida,
            ExistenciaActual = producto.ExistenciaActual,
            ExistenciaMinima = producto.ExistenciaMinima
        };

        return View(formulario);
    }

    [HttpPost]
    // Valida y guarda los cambios del producto indicado.
    public IActionResult Editar(
        [FromRoute] Guid id,
        [FromForm] ProductoFormulario formulario)
    {
        // Revisar el identificador de la ruta por separado de los campos.
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del producto no contiene un identificador válido.");
        }

        // Comprobar que el producto todavía exista.
        if (_almacen.ObtenerProducto(id) is null)
        {
            return this.RegistroNoEncontrado(
                "El producto solicitado no existe o ya fue eliminado.");
        }

        // Conservar los valores enviados cuando haya errores.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.ActualizarProducto(id, formulario);

        // El producto podría desaparecer entre la consulta y la actualización.
        if (resultado.Estado == EstadoOperacion.NoEncontrado)
        {
            return this.RegistroNoEncontrado(resultado.Mensaje);
        }

        if (!resultado.EsExitoso)
        {
            ModelState.AgregarErrores(resultado);

            return View(formulario);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    // Muestra la confirmación antes de eliminar al producto.
    public IActionResult Eliminar([FromRoute] Guid id)
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

        // Mostrar la confirmación sin modificar los registros.
        return View(producto);
    }

    [HttpPost]
    // Ejecuta la eliminación confirmada del producto.
    public IActionResult EliminarConfirmado([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del producto no contiene un identificador válido.");
        }

        // Buscar y eliminar dentro de la misma operación del almacén.
        var resultado = _almacen.EliminarProducto(id);

        if (!resultado.EsExitoso)
        {
            return this.RegistroNoEncontrado(resultado.Mensaje);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        return RedirectToAction(nameof(Index));
    }
}

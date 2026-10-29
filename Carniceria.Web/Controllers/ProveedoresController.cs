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

// Atiende las páginas MVC de consulta y mantenimiento de proveedores.
public class ProveedoresController : Controller
{
    // Acceso al almacén en memoria usado temporalmente por Web.
    private readonly IAlmacenCarniceria _almacen;

    // Recibe el almacén registrado para las acciones del controlador.
    public ProveedoresController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    // Muestra el listado de proveedores.
    public IActionResult Index()
    {
        var proveedores = _almacen.ListarProveedores();

        return View(proveedores);
    }

    [HttpGet]
    // Muestra la ficha del proveedor indicado.
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
    // Presenta el formulario vacío de alta.
    public IActionResult Crear()
    {
        var formulario = new ProveedorFormulario();

        return View(formulario);
    }

    [HttpPost]
    // Valida y guarda el proveedor enviado desde el formulario.
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

    [HttpGet]
    // Busca por identificación exacta o razón social parcial.
    public IActionResult Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        var modelo = new BusquedaViewModel<Proveedor>
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
        else if (modelo.Criterio != "Identificacion" &&
                 modelo.Criterio != "RazonSocial")
        {
            ModelState.AddModelError(
                nameof(modelo.Criterio),
                "Seleccione un criterio de búsqueda válido.");
        }

        if (modelo.Consulta.Length == 0)
        {
            ModelState.AddModelError(
                nameof(modelo.Consulta),
                "Ingrese una identificación o una razón social para buscar.");
        }

        // Conservar el criterio y el texto cuando haya errores.
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // Identificación exacta o todas las coincidencias parciales de razón social.
        modelo.Resultados = modelo.Criterio == "Identificacion"
            ? _almacen.BuscarProveedoresPorIdentificacion(modelo.Consulta)
            : _almacen.BuscarProveedoresPorRazonSocial(modelo.Consulta);

        modelo.BusquedaRealizada = true;

        return View(modelo);
    }

    [HttpGet]
    // Carga los datos del proveedor para editarlo.
    public IActionResult Editar([FromRoute] Guid id)
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

        // Copiar todos los campos editables al modelo del formulario.
        var formulario = new ProveedorFormulario
        {
            TipoIdentificacion = proveedor.TipoIdentificacion,
            Identificacion = proveedor.Identificacion,
            RazonSocial = proveedor.RazonSocial,
            NombreContacto = proveedor.NombreContacto,
            Telefono = proveedor.Telefono,
            CorreoElectronico = proveedor.CorreoElectronico,
            TipoSuministro = proveedor.TipoSuministro,
            FrecuenciaEntrega = proveedor.FrecuenciaEntrega
        };

        return View(formulario);
    }

    [HttpPost]
    // Valida y guarda los cambios del proveedor indicado.
    public IActionResult Editar(
        [FromRoute] Guid id,
        [FromForm] ProveedorFormulario formulario)
    {
        // Revisar el identificador de la ruta por separado de los campos.
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del proveedor no contiene un identificador válido.");
        }

        // Comprobar que el proveedor todavía exista.
        if (_almacen.ObtenerProveedor(id) is null)
        {
            return this.RegistroNoEncontrado(
                "El proveedor solicitado no existe o ya fue eliminado.");
        }

        // Conservar los valores enviados cuando haya errores.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.ActualizarProveedor(id, formulario);

        // El proveedor podría desaparecer entre la consulta y la actualización.
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
    // Muestra la confirmación antes de eliminar al proveedor.
    public IActionResult Eliminar([FromRoute] Guid id)
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

        // Mostrar la confirmación sin modificar los registros.
        return View(proveedor);
    }

    [HttpPost]
    // Ejecuta la eliminación confirmada del proveedor.
    public IActionResult EliminarConfirmado([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del proveedor no contiene un identificador válido.");
        }

        // Buscar y eliminar dentro de la misma operación del almacén.
        var resultado = _almacen.EliminarProveedor(id);

        if (!resultado.EsExitoso)
        {
            return this.RegistroNoEncontrado(resultado.Mensaje);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        return RedirectToAction(nameof(Index));
    }
}

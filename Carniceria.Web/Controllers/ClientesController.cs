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

// Atiende las páginas MVC de consulta y mantenimiento de clientes.
public class ClientesController : Controller
{
    // Acceso al almacén en memoria usado temporalmente por Web.
    private readonly IAlmacenCarniceria _almacen;

    // Recibe el almacén registrado para las acciones del controlador.
    public ClientesController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    // Muestra el listado de clientes.
    public IActionResult Index()
    {
        var clientes = _almacen.ListarClientes();

        return View(clientes);
    }

    [HttpGet]
    // Muestra la ficha del cliente indicado.
    public IActionResult Detalles([FromRoute] Guid id)
    {
        if (id == Guid.Empty || !ModelState.IsValid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del cliente no contiene un identificador válido.");
        }

        var cliente = _almacen.ObtenerCliente(id);

        if (cliente is null)
        {
            return this.RegistroNoEncontrado(
                "El cliente solicitado no existe o ya fue eliminado.");
        }

        return View(cliente);
    }

    [HttpGet]
    // Presenta el formulario vacío de alta.
    public IActionResult Crear()
    {
        var formulario = new ClienteFormulario();

        return View(formulario);
    }

    [HttpPost]
    // Valida y guarda el cliente enviado desde el formulario.
    public IActionResult Crear([FromForm] ClienteFormulario formulario)
    {
        // Conservar el formulario y sus errores cuando los datos sean inválidos.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.CrearCliente(formulario);

        // Incorporar errores del almacén, como una identificación duplicada.
        if (!resultado.EsExitoso)
        {
            ModelState.AgregarErrores(resultado);

            return View(formulario);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        // Redirigir después de guardar para evitar repetir el envío al actualizar.
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    // Busca coincidencias de clientes por identificación.
    public IActionResult Buscar([FromQuery] string? consulta)
    {
        var modelo = new BusquedaViewModel<Cliente>
        {
            Criterio = "Identificacion",
            Consulta = ReglasTexto.Normalizar(consulta)
        };

        // Al abrir la página por primera vez, mostrar las instrucciones.
        if (!Request.Query.ContainsKey(nameof(consulta)))
        {
            return View(modelo);
        }

        // Distinguir una búsqueda vacía de una búsqueda sin coincidencias.
        if (modelo.Consulta.Length == 0)
        {
            ModelState.AddModelError(
                nameof(modelo.Consulta),
                "Ingrese una identificación para buscar.");

            return View(modelo);
        }

        modelo.Resultados = _almacen.BuscarClientesPorIdentificacion(
            modelo.Consulta);

        modelo.BusquedaRealizada = true;

        return View(modelo);
    }

    [HttpGet]
    // Carga los datos del cliente para editarlo.
    public IActionResult Editar([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del cliente no contiene un identificador válido.");
        }

        var cliente = _almacen.ObtenerCliente(id);

        if (cliente is null)
        {
            return this.RegistroNoEncontrado(
                "El cliente solicitado no existe o ya fue eliminado.");
        }

        // Copiar los datos guardados al modelo del formulario.
        var formulario = new ClienteFormulario
        {
            TipoIdentificacion = cliente.TipoIdentificacion,
            Identificacion = cliente.Identificacion,
            Nombre = cliente.Nombre,
            PrimerApellido = cliente.PrimerApellido,
            SegundoApellido = cliente.SegundoApellido,
            FechaNacimiento = cliente.FechaNacimiento
        };

        return View(formulario);
    }

    [HttpPost]
    // Valida y guarda los cambios del cliente indicado.
    public IActionResult Editar(
        [FromRoute] Guid id,
        [FromForm] ClienteFormulario formulario)
    {
        // Revisar el identificador de la ruta por separado de los campos.
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del cliente no contiene un identificador válido.");
        }

        // Comprobar que el cliente todavía exista.
        if (_almacen.ObtenerCliente(id) is null)
        {
            return this.RegistroNoEncontrado(
                "El cliente solicitado no existe o ya fue eliminado.");
        }

        // Conservar los valores enviados cuando haya errores.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.ActualizarCliente(id, formulario);

        // El cliente podría desaparecer entre la consulta y la actualización.
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
    // Muestra la confirmación antes de eliminar al cliente.
    public IActionResult Eliminar([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del cliente no contiene un identificador válido.");
        }

        var cliente = _almacen.ObtenerCliente(id);

        if (cliente is null)
        {
            return this.RegistroNoEncontrado(
                "El cliente solicitado no existe o ya fue eliminado.");
        }

        // Mostrar la confirmación sin modificar los registros.
        return View(cliente);
    }

    [HttpPost]
    // Ejecuta la eliminación confirmada del cliente.
    public IActionResult EliminarConfirmado([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) == ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del cliente no contiene un identificador válido.");
        }

        // El almacén busca y elimina el registro dentro de la misma operación.
        var resultado = _almacen.EliminarCliente(id);

        // La eliminación falla si el cliente ya no existe.
        if (!resultado.EsExitoso)
        {
            return this.RegistroNoEncontrado(resultado.Mensaje);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        return RedirectToAction(nameof(Index));
    }
}

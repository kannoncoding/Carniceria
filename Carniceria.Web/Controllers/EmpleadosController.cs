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

    [HttpGet]
    public IActionResult Crear()
    {
        var formulario = new EmpleadoFormulario();

        return View(formulario);
    }

    [HttpPost]
    public IActionResult Crear([FromForm] EmpleadoFormulario formulario)
    {
        // Conservar los valores enviados y mostrar los errores del formulario.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.CrearEmpleado(formulario);

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
    public IActionResult Buscar([FromQuery] string? consulta)
    {
        var modelo = new BusquedaViewModel<Empleado>
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

        modelo.Resultados = _almacen.BuscarEmpleadosPorIdentificacion(
            modelo.Consulta);

        modelo.BusquedaRealizada = true;

        return View(modelo);
    }

    [HttpGet]
    public IActionResult Editar([FromRoute] Guid id)
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

        // Copiar todos los campos editables al modelo del formulario.
        var formulario = new EmpleadoFormulario
        {
            TipoIdentificacion = empleado.TipoIdentificacion,
            Identificacion = empleado.Identificacion,
            Nombre = empleado.Nombre,
            PrimerApellido = empleado.PrimerApellido,
            SegundoApellido = empleado.SegundoApellido,
            FechaNacimiento = empleado.FechaNacimiento,
            SalarioMensual = empleado.SalarioMensual,
            FechaIngreso = empleado.FechaIngreso,
            AreaTrabajo = empleado.AreaTrabajo,
            Puesto = empleado.Puesto,
            Turno = empleado.Turno
        };

        return View(formulario);
    }

    [HttpPost]
    public IActionResult Editar(
        [FromRoute] Guid id,
        [FromForm] EmpleadoFormulario formulario)
    {
        // Revisar el identificador de la ruta por separado de los campos.
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del empleado no contiene un identificador válido.");
        }

        // Comprobar que el empleado todavía exista.
        if (_almacen.ObtenerEmpleado(id) is null)
        {
            return this.RegistroNoEncontrado(
                "El empleado solicitado no existe o ya fue eliminado.");
        }

        // Conservar los valores enviados cuando haya errores.
        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = _almacen.ActualizarEmpleado(id, formulario);

        // El empleado podría desaparecer entre la consulta y la actualización.
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
    public IActionResult Eliminar([FromRoute] Guid id)
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

        // Mostrar la confirmación sin modificar los registros.
        return View(empleado);
    }

    [HttpPost]
    public IActionResult EliminarConfirmado([FromRoute] Guid id)
    {
        if (id == Guid.Empty ||
            ModelState.GetFieldValidationState(nameof(id)) ==
                ModelValidationState.Invalid)
        {
            return this.RegistroNoEncontrado(
                "El enlace del empleado no contiene un identificador válido.");
        }

        // Buscar y eliminar dentro de la misma operación del almacén.
        var resultado = _almacen.EliminarEmpleado(id);

        if (!resultado.EsExitoso)
        {
            return this.RegistroNoEncontrado(resultado.Mensaje);
        }

        TempData["MensajeExito"] = resultado.Mensaje;

        return RedirectToAction(nameof(Index));
    }
}
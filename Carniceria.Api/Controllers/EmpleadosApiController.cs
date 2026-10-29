using System;
using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Mapeos;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/empleados")]
public class EmpleadosApiController : ControladorApiBase
{
    private readonly IAlmacenCarniceria _almacen;

    public EmpleadosApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var empleados = _almacen.ListarEmpleados()
            .Select(empleado => MapeadorRespuestas.Convertir(empleado))
            .ToArray();

        return Ok(empleados);
    }

    [HttpGet("buscar")]
    public IActionResult Buscar(
        [FromQuery(Name = "identificacion")] string? identificacion)
    {
        var empleados = _almacen.BuscarEmpleadosPorIdentificacion(
                identificacion ?? string.Empty)
            .Select(empleado => MapeadorRespuestas.Convertir(empleado))
            .ToArray();

        return Ok(empleados);
    }

    [HttpGet("{id:guid}")]
    public IActionResult ObtenerPorId(Guid id)
    {
        var empleado = _almacen.ObtenerEmpleado(id);

        return empleado is null
            ? CrearNoEncontrado("El empleado solicitado no existe.")
            : Ok(MapeadorRespuestas.Convertir(empleado));
    }

    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudEmpleado solicitud)
    {
        var alta = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.CrearEmpleado(solicitud);
            var empleado = resultado.EsExitoso
                ? _almacen.ObtenerEmpleado(resultado.Id!.Value)
                : null;
            var respuesta = empleado is null
                ? null
                : MapeadorRespuestas.Convertir(empleado);

            return (Resultado: resultado, Respuesta: respuesta);
        });

        if (!alta.Resultado.EsExitoso)
        {
            return ConvertirResultadoFallido(alta.Resultado);
        }

        return CreatedAtAction(
            nameof(ObtenerPorId),
            new { id = alta.Respuesta!.Id },
            alta.Respuesta);
    }

    [HttpPut("{id:guid}")]
    public IActionResult Actualizar(
        Guid id,
        [FromBody] SolicitudEmpleado solicitud)
    {
        var actualizacion = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.ActualizarEmpleado(id, solicitud);
            var empleado = resultado.EsExitoso
                ? _almacen.ObtenerEmpleado(id)
                : null;
            var respuesta = empleado is null
                ? null
                : MapeadorRespuestas.Convertir(empleado);

            return (Resultado: resultado, Respuesta: respuesta);
        });

        if (!actualizacion.Resultado.EsExitoso)
        {
            return ConvertirResultadoFallido(actualizacion.Resultado);
        }

        return actualizacion.Respuesta is null
            ? CrearNoEncontrado("El empleado solicitado no existe.")
            : Ok(actualizacion.Respuesta);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Eliminar(Guid id)
    {
        var resultado = _almacen.EliminarEmpleado(id);

        return resultado.EsExitoso
            ? NoContent()
            : ConvertirResultadoFallido(resultado);
    }
}

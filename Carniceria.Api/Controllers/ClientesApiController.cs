using System;
using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Mapeos;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesApiController : ControladorApiBase
{
    private readonly IAlmacenCarniceria _almacen;

    public ClientesApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var clientes = _almacen.ListarClientes()
            .Select(cliente => MapeadorRespuestas.Convertir(cliente))
            .ToArray();

        return Ok(clientes);
    }

    [HttpGet("buscar")]
    public IActionResult Buscar(
        [FromQuery(Name = "identificacion")] string? identificacion)
    {
        var clientes = _almacen.BuscarClientesPorIdentificacion(
                identificacion ?? string.Empty)
            .Select(cliente => MapeadorRespuestas.Convertir(cliente))
            .ToArray();

        return Ok(clientes);
    }

    [HttpGet("{id:guid}")]
    public IActionResult ObtenerPorId(Guid id)
    {
        var cliente = _almacen.ObtenerCliente(id);

        return cliente is null
            ? CrearNoEncontrado("El cliente solicitado no existe.")
            : Ok(MapeadorRespuestas.Convertir(cliente));
    }

    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudCliente solicitud)
    {
        var alta = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.CrearCliente(solicitud);
            var cliente = resultado.EsExitoso
                ? _almacen.ObtenerCliente(resultado.Id!.Value)
                : null;
            var respuesta = cliente is null
                ? null
                : MapeadorRespuestas.Convertir(cliente);

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
        [FromBody] SolicitudCliente solicitud)
    {
        var actualizacion = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.ActualizarCliente(id, solicitud);
            var cliente = resultado.EsExitoso
                ? _almacen.ObtenerCliente(id)
                : null;
            var respuesta = cliente is null
                ? null
                : MapeadorRespuestas.Convertir(cliente);

            return (Resultado: resultado, Respuesta: respuesta);
        });

        if (!actualizacion.Resultado.EsExitoso)
        {
            return ConvertirResultadoFallido(actualizacion.Resultado);
        }

        return actualizacion.Respuesta is null
            ? CrearNoEncontrado("El cliente solicitado no existe.")
            : Ok(actualizacion.Respuesta);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Eliminar(Guid id)
    {
        var resultado = _almacen.EliminarCliente(id);

        return resultado.EsExitoso
            ? NoContent()
            : ConvertirResultadoFallido(resultado);
    }
}

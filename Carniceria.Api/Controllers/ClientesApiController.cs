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
public class ClientesApiController : ControllerBase
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

    [HttpGet("{id:guid}")]
    public IActionResult ObtenerPorId(Guid id)
    {
        var cliente = _almacen.ObtenerCliente(id);

        if (cliente is null)
        {
            return NotFound(CrearErrorNoEncontrado(
                "El cliente solicitado no existe."));
        }

        return Ok(MapeadorRespuestas.Convertir(cliente));
    }

    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudCliente solicitud)
    {
        var resultado = _almacen.CrearCliente(solicitud);

        if (!resultado.EsExitoso)
        {
            return ConvertirResultado(resultado);
        }

        var cliente = _almacen.ObtenerCliente(resultado.Id!.Value);
        var respuesta = MapeadorRespuestas.Convertir(cliente!);

        return CreatedAtAction(
            nameof(ObtenerPorId),
            new { id = respuesta.Id },
            respuesta);
    }

    private IActionResult ConvertirResultado(
        Carniceria.Modelos.Resultados.ResultadoOperacion resultado)
    {
        var error = MapeadorErroresApi.Convertir(resultado);

        return resultado.Estado switch
        {
            EstadoOperacion.Duplicado => Conflict(error),
            EstadoOperacion.NoEncontrado => NotFound(error),
            _ => BadRequest(error)
        };
    }

    private static ErrorApi CrearErrorNoEncontrado(string mensaje)
    {
        return new ErrorApi
        {
            Codigo = "NoEncontrado",
            Mensaje = mensaje
        };
    }
}

using System;
using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Mapeos;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/productos")]
public class ProductosApiController : ControllerBase
{
    private readonly IAlmacenCarniceria _almacen;

    public ProductosApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var productos = _almacen.ListarProductos()
            .Select(MapearProducto)
            .ToArray();

        return Ok(productos);
    }

    [HttpGet("{id:guid}")]
    public IActionResult ObtenerPorId(Guid id)
    {
        var producto = _almacen.ObtenerProducto(id);

        if (producto is null)
        {
            return NotFound(CrearErrorNoEncontrado(
                "El producto solicitado no existe."));
        }

        var proveedor = _almacen.ObtenerProveedor(producto.ProveedorId);
        if (proveedor is null)
        {
            return NotFound(CrearErrorNoEncontrado(
                "El proveedor relacionado con el producto no existe."));
        }

        return Ok(MapeadorRespuestas.Convertir(producto, proveedor));
    }

    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudProducto solicitud)
    {
        var resultado = _almacen.CrearProducto(solicitud);

        if (!resultado.EsExitoso)
        {
            return ConvertirResultado(resultado);
        }

        var producto = _almacen.ObtenerProducto(resultado.Id!.Value);
        var respuesta = MapearProducto(producto!);

        return CreatedAtAction(
            nameof(ObtenerPorId),
            new { id = respuesta.Id },
            respuesta);
    }

    private RespuestaProducto MapearProducto(Producto producto)
    {
        var proveedor = _almacen.ObtenerProveedor(producto.ProveedorId);

        if (proveedor is null)
        {
            throw new InvalidOperationException(
                "El producto no tiene un proveedor relacionado válido.");
        }

        return MapeadorRespuestas.Convertir(producto, proveedor);
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

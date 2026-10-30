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

// Expone operaciones HTTP y datos descriptivos del proveedor de cada producto.
[ApiController]
[Route("api/productos")]
public class ProductosApiController : ControladorApiBase
{
    // Servicio de datos compartido por las solicitudes del controlador.
    private readonly IAlmacenCarniceria _almacen;

    // Recibe el almacen singleton registrado en la aplicacion.
    public ProductosApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    // Devuelve productos junto con su proveedor dentro del mismo bloqueo.
    [HttpGet]
    public IActionResult Listar()
    {
        var productos = _almacen.EjecutarConBloqueo(() =>
            _almacen.ListarProductos()
                .Select(MapearProducto)
                .ToArray());

        return Ok(productos);
    }

    // Busca por codigo exacto o nombre parcial segun el criterio indicado.
    [HttpGet("buscar")]
    public IActionResult Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        var criterioNormalizado = criterio?.Trim();

        if (!string.Equals(
                criterioNormalizado,
                "Codigo",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                criterioNormalizado,
                "Nombre",
                StringComparison.OrdinalIgnoreCase))
        {
            return CrearCriterioInvalido("Criterio");
        }

        var productos = _almacen.EjecutarConBloqueo(() =>
        {
            var encontrados = string.Equals(
                criterioNormalizado,
                "Codigo",
                StringComparison.OrdinalIgnoreCase)
                ? _almacen.BuscarProductosPorCodigo(consulta ?? string.Empty)
                : _almacen.BuscarProductosPorNombre(consulta ?? string.Empty);

            return encontrados
                .Select(MapearProducto)
                .ToArray();
        });

        return Ok(productos);
    }

    // Obtiene el producto y su proveedor asociado por identificador.
    [HttpGet("{id:guid}")]
    public IActionResult ObtenerPorId(Guid id)
    {
        var respuesta = _almacen.EjecutarConBloqueo(() =>
        {
            var producto = _almacen.ObtenerProducto(id);
            return producto is null ? null : MapearProducto(producto);
        });

        return respuesta is null
            ? CrearNoEncontrado("El producto solicitado no existe.")
            : Ok(respuesta);
    }

    // Crea el producto y devuelve la ruta de consulta del registro.
    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudProducto solicitud)
    {
        var alta = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.CrearProducto(solicitud);
            var producto = resultado.EsExitoso
                ? _almacen.ObtenerProducto(resultado.Id!.Value)
                : null;
            var respuesta = producto is null
                ? null
                : MapearProducto(producto);

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

    // Actualiza el producto usando el identificador de la ruta.
    [HttpPut("{id:guid}")]
    public IActionResult Actualizar(
        Guid id,
        [FromBody] SolicitudProducto solicitud)
    {
        var actualizacion = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.ActualizarProducto(id, solicitud);
            var producto = resultado.EsExitoso
                ? _almacen.ObtenerProducto(id)
                : null;
            var respuesta = producto is null
                ? null
                : MapearProducto(producto);

            return (Resultado: resultado, Respuesta: respuesta);
        });

        if (!actualizacion.Resultado.EsExitoso)
        {
            return ConvertirResultadoFallido(actualizacion.Resultado);
        }

        return actualizacion.Respuesta is null
            ? CrearNoEncontrado("El producto solicitado no existe.")
            : Ok(actualizacion.Respuesta);
    }

    // Elimina el producto indicado o devuelve el resultado fallido.
    [HttpDelete("{id:guid}")]
    public IActionResult Eliminar(Guid id)
    {
        var resultado = _almacen.EliminarProducto(id);

        return resultado.EsExitoso
            ? NoContent()
            : ConvertirResultadoFallido(resultado);
    }

    // Crea una respuesta coherente con los datos actuales del proveedor.
    private RespuestaProducto MapearProducto(Producto producto)
    {
        var proveedor = _almacen.ObtenerProveedor(producto.ProveedorId)
            ?? throw new InvalidOperationException(
                "El producto no tiene un proveedor relacionado válido.");

        return MapeadorRespuestas.Convertir(producto, proveedor);
    }
}

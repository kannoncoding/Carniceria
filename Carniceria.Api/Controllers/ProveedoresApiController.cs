using System;
using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Mapeos;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/proveedores")]
public class ProveedoresApiController : ControladorApiBase
{
    private readonly IAlmacenCarniceria _almacen;

    public ProveedoresApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var proveedores = _almacen.ListarProveedores()
            .Select(proveedor => MapeadorRespuestas.Convertir(proveedor))
            .ToArray();

        return Ok(proveedores);
    }

    [HttpGet("buscar")]
    public IActionResult Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        if (string.Equals(
                criterio?.Trim(),
                "Identificacion",
                StringComparison.OrdinalIgnoreCase))
        {
            var proveedores = _almacen.BuscarProveedoresPorIdentificacion(
                    consulta ?? string.Empty)
                .Select(proveedor => MapeadorRespuestas.Convertir(proveedor))
                .ToArray();

            return Ok(proveedores);
        }

        if (string.Equals(
                criterio?.Trim(),
                "RazonSocial",
                StringComparison.OrdinalIgnoreCase))
        {
            var proveedores = _almacen.BuscarProveedoresPorRazonSocial(
                    consulta ?? string.Empty)
                .Select(proveedor => MapeadorRespuestas.Convertir(proveedor))
                .ToArray();

            return Ok(proveedores);
        }

        return CrearCriterioInvalido("Criterio");
    }

    [HttpGet("{id:guid}")]
    public IActionResult ObtenerPorId(Guid id)
    {
        var proveedor = _almacen.ObtenerProveedor(id);

        return proveedor is null
            ? CrearNoEncontrado("El proveedor solicitado no existe.")
            : Ok(MapeadorRespuestas.Convertir(proveedor));
    }

    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudProveedor solicitud)
    {
        var alta = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.CrearProveedor(solicitud);
            var proveedor = resultado.EsExitoso
                ? _almacen.ObtenerProveedor(resultado.Id!.Value)
                : null;
            var respuesta = proveedor is null
                ? null
                : MapeadorRespuestas.Convertir(proveedor);

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
        [FromBody] SolicitudProveedor solicitud)
    {
        var actualizacion = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.ActualizarProveedor(id, solicitud);
            var proveedor = resultado.EsExitoso
                ? _almacen.ObtenerProveedor(id)
                : null;
            var respuesta = proveedor is null
                ? null
                : MapeadorRespuestas.Convertir(proveedor);

            return (Resultado: resultado, Respuesta: respuesta);
        });

        if (!actualizacion.Resultado.EsExitoso)
        {
            return ConvertirResultadoFallido(actualizacion.Resultado);
        }

        return actualizacion.Respuesta is null
            ? CrearNoEncontrado("El proveedor solicitado no existe.")
            : Ok(actualizacion.Respuesta);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Eliminar(Guid id)
    {
        var resultado = _almacen.EliminarProveedor(id);

        return resultado.EsExitoso
            ? NoContent()
            : ConvertirResultadoFallido(resultado);
    }
}

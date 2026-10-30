using System;
using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Reglas;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

// Publica el registro y las consultas de facturas.
[ApiController]
[Route("api/facturas")]
public class FacturasApiController : ControladorApiBase
{
    // Servicio de datos compartido por las solicitudes del controlador.
    private readonly IAlmacenCarniceria _almacen;

    // Recibe el almacén singleton registrado en la aplicación.
    public FacturasApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    // Devuelve los porcentajes vigentes como valores decimales JSON.
    [HttpGet("configuracion")]
    public IActionResult ObtenerConfiguracion()
    {
        return Ok(new RespuestaConfiguracionFacturas
        {
            PorcentajeDescuentoMaximo = ReglasFacturacion.PorcentajeDescuentoMaximo,
            PorcentajeIva = ReglasFacturacion.PorcentajeIva
        });
    }

    // Devuelve las facturas con los datos descriptivos actuales relacionados.
    [HttpGet]
    public IActionResult Listar()
    {
        var facturas = _almacen.EjecutarConBloqueo(() =>
            _almacen.ListarFacturas()
                .Select(MapearFactura)
                .ToArray());

        return Ok(facturas);
    }

    // Busca por número exacto o identificación exacta del cliente.
    [HttpGet("buscar")]
    public IActionResult Buscar(
        [FromQuery] string? criterio,
        [FromQuery] string? consulta)
    {
        var criterioNormalizado = criterio?.Trim();
        if (!string.Equals(
                criterioNormalizado,
                "Numero",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                criterioNormalizado,
                "IdentificacionCliente",
                StringComparison.OrdinalIgnoreCase))
        {
            return CrearCriterioInvalido("Criterio");
        }

        var facturas = _almacen.EjecutarConBloqueo(() =>
        {
            var encontradas = string.Equals(
                criterioNormalizado,
                "Numero",
                StringComparison.OrdinalIgnoreCase)
                ? _almacen.BuscarFacturasPorNumero(consulta ?? string.Empty)
                : _almacen.BuscarFacturasPorIdentificacionCliente(consulta ?? string.Empty);

            return encontradas
                .Select(MapearFactura)
                .ToArray();
        });

        return Ok(facturas);
    }

    // Obtiene una factura por su número exacto.
    [HttpGet("{numero}")]
    public IActionResult ObtenerPorNumero(string numero)
    {
        var factura = _almacen.EjecutarConBloqueo(() =>
        {
            var encontrada = _almacen.ObtenerFacturaPorNumero(numero);
            return encontrada is null
                ? null
                : MapearFactura(encontrada);
        });

        return factura is null
            ? CrearNoEncontrado("La factura solicitada no existe.")
            : Ok(factura);
    }

    // Registra una factura calculada y devuelve su ruta de consulta.
    [HttpPost]
    public IActionResult Crear([FromBody] SolicitudFactura solicitud)
    {
        var alta = _almacen.EjecutarConBloqueo(() =>
        {
            var resultado = _almacen.CrearFactura(solicitud);
            var factura = resultado.EsExitoso
                ? _almacen.ObtenerFactura(resultado.Id!.Value)
                : null;
            var respuesta = factura is null
                ? null
                : MapearFactura(factura);

            return (Resultado: resultado, Respuesta: respuesta);
        });

        if (!alta.Resultado.EsExitoso)
        {
            return ConvertirResultadoFallido(alta.Resultado);
        }

        if (alta.Respuesta is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorApi
            {
                Codigo = "ErrorInterno",
                Mensaje = "No se pudo completar la solicitud."
            });
        }

        return CreatedAtAction(
            nameof(ObtenerPorNumero),
            new { numero = alta.Respuesta.Numero },
            alta.Respuesta);
    }

    // Proyecta una copia de factura y sus relaciones mientras se mantiene el bloqueo.
    private RespuestaFactura MapearFactura(Factura factura)
    {
        var cliente = _almacen.ObtenerCliente(factura.ClienteId)
            ?? throw new InvalidOperationException("La factura no tiene un cliente disponible.");
        var empleado = _almacen.ObtenerEmpleado(factura.EmpleadoId)
            ?? throw new InvalidOperationException("La factura no tiene un empleado disponible.");

        var detalles = factura.Detalles
            .Select(detalle =>
            {
                var producto = _almacen.ObtenerProducto(detalle.ProductoId)
                    ?? throw new InvalidOperationException("La factura no tiene un producto disponible.");

                return new RespuestaDetalleFactura
                {
                    Id = detalle.Id,
                    ProductoId = detalle.ProductoId,
                    ProductoCodigo = producto.Codigo,
                    ProductoNombre = producto.Nombre,
                    UnidadMedida = detalle.UnidadMedida,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = detalle.PrecioUnitario,
                    SubtotalLinea = detalle.SubtotalLinea
                };
            })
            .ToArray();

        return new RespuestaFactura
        {
            Id = factura.Id,
            Numero = factura.Numero,
            FechaEmision = factura.FechaEmision,
            ClienteId = factura.ClienteId,
            ClienteIdentificacion = cliente.Identificacion,
            ClienteNombreCompleto = string.Join(
                " ",
                new[] { cliente.Nombre, cliente.PrimerApellido, cliente.SegundoApellido }
                    .Where(parte => !string.IsNullOrWhiteSpace(parte))),
            EmpleadoId = factura.EmpleadoId,
            EmpleadoIdentificacion = empleado.Identificacion,
            EmpleadoNombreCompleto = string.Join(
                " ",
                new[] { empleado.Nombre, empleado.PrimerApellido, empleado.SegundoApellido }
                    .Where(parte => !string.IsNullOrWhiteSpace(parte))),
            MetodoPago = factura.MetodoPago,
            PorcentajeDescuento = factura.PorcentajeDescuento,
            Estado = factura.Estado,
            Subtotal = factura.Subtotal,
            MontoDescuento = factura.MontoDescuento,
            BaseGravable = factura.BaseGravable,
            MontoIva = factura.MontoIva,
            Total = factura.Total,
            Detalles = Array.AsReadOnly(detalles)
        };
    }
}

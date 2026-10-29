using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    // Copia precios y unidades y comprueba la demanda total sin cambiar existencias.
    private (ResultadoOperacion? Error, IReadOnlyList<DetalleFactura> Detalles)
        PrepararDetallesFactura(SolicitudFactura solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        return EjecutarConBloqueo(() =>
        {
            var erroresValidacion = ValidarSolicitudFactura(solicitud);
            if (erroresValidacion.Count > 0)
            {
                return (
                    ResultadoOperacion.ValidacionFallida(erroresValidacion),
                    (IReadOnlyList<DetalleFactura>)Array.Empty<DetalleFactura>());
            }

            var demandaPorProducto = new Dictionary<Guid, decimal>();
            for (var indice = 0; indice < solicitud.Detalles.Count; indice++)
            {
                var detalle = solicitud.Detalles[indice];
                try
                {
                    demandaPorProducto[detalle.ProductoId] =
                        demandaPorProducto.GetValueOrDefault(detalle.ProductoId) + detalle.Cantidad;
                }
                catch (OverflowException)
                {
                    var errorDemanda = new ValidationResult(
                        "La suma de las cantidades solicitadas excede el rango permitido.",
                        new[] { $"Detalles[{indice}].{nameof(detalle.Cantidad)}" });
                    return (
                        ResultadoOperacion.ValidacionFallida(new[] { errorDemanda }),
                        (IReadOnlyList<DetalleFactura>)Array.Empty<DetalleFactura>());
                }
            }

            foreach (var demanda in demandaPorProducto)
            {
                var producto = _productos.First(producto => producto.Id == demanda.Key);
                if (demanda.Value > producto.ExistenciaActual)
                {
                    var indice = solicitud.Detalles.FindIndex(
                        detalle => detalle.ProductoId == producto.Id);
                    var conflicto = ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indice}].{nameof(SolicitudDetalleFactura.Cantidad)}",
                        $"La existencia de {producto.Nombre} no cubre la cantidad total solicitada.");
                    return (
                        conflicto,
                        (IReadOnlyList<DetalleFactura>)Array.Empty<DetalleFactura>());
                }
            }

            // Cada línea conserva su cantidad y usa datos actuales del producto.
            var detallesPreparados = solicitud.Detalles
                .Select(detalle =>
                {
                    var producto = _productos.First(
                        producto => producto.Id == detalle.ProductoId);
                    return new DetalleFactura
                    {
                        ProductoId = producto.Id,
                        UnidadMedida = producto.UnidadMedida,
                        Cantidad = detalle.Cantidad,
                        PrecioUnitario = producto.PrecioUnidadMedida
                    };
                })
                .ToArray();

            return (
                (ResultadoOperacion?)null,
                (IReadOnlyList<DetalleFactura>)Array.AsReadOnly(detallesPreparados));
        });
    }

    // Asigna identidad a un candidato validado bajo el bloqueo del almacén.
    private Factura PrepararFacturaValidadaParaGuardar(Factura candidata)
    {
        ArgumentNullException.ThrowIfNull(candidata);
        ArgumentNullException.ThrowIfNull(candidata.Detalles);

        return EjecutarConBloqueo(() =>
        {
            var factura = candidata.Copiar();
            factura.Numero = "FAC" + _siguienteNumeroFactura.ToString(
                "D6",
                CultureInfo.InvariantCulture);
            factura.FechaEmision = TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica"));
            factura.Estado = EstadoFactura.Pendiente;
            factura.Detalles = candidata.Detalles
                .Select(detalle => new DetalleFactura
                {
                    ProductoId = detalle.ProductoId,
                    UnidadMedida = detalle.UnidadMedida,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = detalle.PrecioUnitario,
                    SubtotalLinea = detalle.SubtotalLinea
                })
                .ToList();

            _siguienteNumeroFactura++;
            return factura;
        });
    }
}

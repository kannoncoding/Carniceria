using System.Collections.Generic;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Solicitudes;

// Datos editables de una factura existente.
public sealed class SolicitudEdicionFactura
{
    // Relaciones y valores elegidos para la cabecera.
    public Guid ClienteId { get; init; }

    public Guid EmpleadoId { get; init; }

    public MetodoPago MetodoPago { get; init; }

    public decimal PorcentajeDescuento { get; init; }

    // Indica si las lineas retenidas toman precio y unidad actuales.
    public bool ActualizarPreciosYUnidades { get; init; }

    // Lineas solicitadas con Id opcional para conservar las existentes.
    public List<SolicitudDetalleEdicionFactura?>? Detalles { get; init; } = new();
}

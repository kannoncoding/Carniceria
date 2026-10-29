using System;
using System.Collections.Generic;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Solicitudes;

// Datos editables que recibe la API al crear una factura.
public sealed class SolicitudFactura
{
    // Relaciones y opciones definidas por el formulario.
    public Guid ClienteId { get; init; }

    public Guid EmpleadoId { get; init; }

    public MetodoPago MetodoPago { get; init; }

    public decimal PorcentajeDescuento { get; init; }

    // Productos y cantidades sin valores calculados por el cliente.
    public List<SolicitudDetalleFactura> Detalles { get; init; } = new();
}

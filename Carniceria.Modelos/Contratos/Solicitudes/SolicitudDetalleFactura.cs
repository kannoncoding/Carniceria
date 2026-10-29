using System;

namespace Carniceria.Modelos.Contratos.Solicitudes;

// Datos editables de una línea nueva de factura.
public sealed class SolicitudDetalleFactura
{
    // Producto y cantidad solicitados.
    public Guid ProductoId { get; init; }

    public decimal Cantidad { get; init; }
}

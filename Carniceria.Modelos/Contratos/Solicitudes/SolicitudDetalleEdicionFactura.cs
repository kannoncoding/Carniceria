namespace Carniceria.Modelos.Contratos.Solicitudes;

// Datos editables de una linea de factura.
public sealed class SolicitudDetalleEdicionFactura
{
    // Id opcional de una linea que ya pertenece a la factura.
    public Guid? Id { get; init; }

    // Producto y cantidad pedidos para la linea.
    public Guid ProductoId { get; init; }

    public decimal Cantidad { get; init; }
}

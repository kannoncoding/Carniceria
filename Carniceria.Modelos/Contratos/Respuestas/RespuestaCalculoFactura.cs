using System.Collections.Generic;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Importes y lineas de calculo sin numero, fecha ni Id de factura.
public sealed class RespuestaCalculoFactura
{
    // Porcentaje solicitado y valores monetarios calculados.
    public decimal PorcentajeDescuento { get; init; }

    public decimal Subtotal { get; init; }

    public decimal MontoDescuento { get; init; }

    public decimal BaseGravable { get; init; }

    public decimal MontoIva { get; init; }

    public decimal Total { get; init; }

    // Lineas descriptivas sin Id de detalle asignado.
    public IReadOnlyList<RespuestaDetalleCalculoFactura> Detalles { get; init; } =
        Array.Empty<RespuestaDetalleCalculoFactura>();
}

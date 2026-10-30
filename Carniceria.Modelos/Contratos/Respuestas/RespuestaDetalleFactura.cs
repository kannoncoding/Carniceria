using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Datos calculados y descriptivos de una linea de factura.
public sealed class RespuestaDetalleFactura
{
    // Identificadores de linea y producto.
    public Guid Id { get; init; }

    public Guid ProductoId { get; init; }

    // Datos descriptivos del producto y la unidad de la linea.
    public string ProductoCodigo { get; init; } = string.Empty;

    public string ProductoNombre { get; init; } = string.Empty;

    public UnidadMedida UnidadMedida { get; init; }

    // Cantidad y valores monetarios calculados por la API.
    public decimal Cantidad { get; init; }

    public decimal PrecioUnitario { get; init; }

    public decimal SubtotalLinea { get; init; }
}

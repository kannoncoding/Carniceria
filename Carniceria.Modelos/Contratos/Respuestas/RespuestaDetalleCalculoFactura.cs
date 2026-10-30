using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Linea descriptiva de una factura calculada sin identidad definitiva.
public sealed class RespuestaDetalleCalculoFactura
{
    // Referencia del producto y datos descriptivos actuales.
    public Guid ProductoId { get; init; }

    public string ProductoCodigo { get; init; } = string.Empty;

    public string ProductoNombre { get; init; } = string.Empty;

    public UnidadMedida UnidadMedida { get; init; }

    // Cantidad y valores monetarios calculados por la API.
    public decimal Cantidad { get; init; }

    public decimal PrecioUnitario { get; init; }

    public decimal SubtotalLinea { get; init; }
}

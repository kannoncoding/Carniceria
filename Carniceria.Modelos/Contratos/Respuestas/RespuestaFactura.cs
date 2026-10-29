using System;
using System.Collections.Generic;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Cabecera y líneas calculadas de una factura.
public sealed class RespuestaFactura
{
    // Identificador, número y fecha asignados por la API.
    public Guid Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public DateTimeOffset FechaEmision { get; init; }

    // Relaciones con datos descriptivos de cliente y empleado.
    public Guid ClienteId { get; init; }

    public string ClienteIdentificacion { get; init; } = string.Empty;

    public string ClienteNombreCompleto { get; init; } = string.Empty;

    public Guid EmpleadoId { get; init; }

    public string EmpleadoIdentificacion { get; init; } = string.Empty;

    public string EmpleadoNombreCompleto { get; init; } = string.Empty;

    // Método de pago, descuento y estado actual.
    public MetodoPago MetodoPago { get; init; }

    public decimal PorcentajeDescuento { get; init; }

    public EstadoFactura Estado { get; init; }

    // Importes calculados y detalle de productos.
    public decimal Subtotal { get; init; }

    public decimal MontoDescuento { get; init; }

    public decimal BaseGravable { get; init; }

    public decimal MontoIva { get; init; }

    public decimal Total { get; init; }

    public IReadOnlyList<RespuestaDetalleFactura> Detalles { get; init; } =
        Array.Empty<RespuestaDetalleFactura>();
}

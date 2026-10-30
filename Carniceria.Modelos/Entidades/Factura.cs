using System;
using System.Collections.Generic;
using System.Linq;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

// Agrupa la cabecera, los importes y las lineas de una factura.
public class Factura
{
    // Identificador asignado al crear la factura.
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Datos de emision y relaciones con cliente y empleado.
    public string Numero { get; set; } = string.Empty;

    public DateTimeOffset FechaEmision { get; set; }

    public Guid ClienteId { get; set; }

    public Guid EmpleadoId { get; set; }

    // Metodo y estado actual de la factura.
    public MetodoPago MetodoPago { get; set; }

    public decimal PorcentajeDescuento { get; set; }

    public EstadoFactura Estado { get; set; } = EstadoFactura.Pendiente;

    // Importes calculados para la cabecera.
    public decimal Subtotal { get; set; }

    public decimal MontoDescuento { get; set; }

    public decimal BaseGravable { get; set; }

    public decimal MontoIva { get; set; }

    public decimal Total { get; set; }

    // Lineas asociadas a esta factura.
    public List<DetalleFactura> Detalles { get; set; } = new();

    // Copia la cabecera y clona cada linea para evitar referencias compartidas.
    public Factura Copiar()
    {
        var copia = (Factura)MemberwiseClone();
        copia.Detalles = Detalles.Select(detalle => detalle.Copiar()).ToList();
        return copia;
    }
}

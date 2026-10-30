using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

// Representa una linea con producto e importes capturados.
public class DetalleFactura
{
    // Identificador asignado al crear la linea.
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Producto y unidad usados en la factura.
    public Guid ProductoId { get; set; }

    public UnidadMedida UnidadMedida { get; set; }

    // Cantidad y valores monetarios de la linea.
    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal SubtotalLinea { get; set; }

    // Copia la linea con su identidad y datos de valor.
    public DetalleFactura Copiar()
    {
        return (DetalleFactura)MemberwiseClone();
    }
}

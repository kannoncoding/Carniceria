using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

// Métodos disponibles para registrar una factura.
public enum MetodoPago
{
    Efectivo = 1,
    Tarjeta = 2,

    [Display(Name = "SINPE Móvil")]
    SinpeMovil = 3,

    Transferencia = 4
}

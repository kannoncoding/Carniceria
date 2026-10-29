using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

// Unidades admitidas para expresar precios y existencias.
public enum UnidadMedida
{
    [Display(Name = "Kilogramo")]
    Kilogramo = 1,

    [Display(Name = "Libra")]
    Libra = 2,

    [Display(Name = "Unidad")]
    Unidad = 3,

    [Display(Name = "Paquete")]
    Paquete = 4,

    [Display(Name = "Bandeja")]
    Bandeja = 5
}

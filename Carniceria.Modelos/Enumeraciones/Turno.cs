using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

public enum Turno
{
    [Display(Name = "Mañana")]
    Manana = 1,

    [Display(Name = "Tarde")]
    Tarde = 2,

    [Display(Name = "Mixto")]
    Mixto = 3
}
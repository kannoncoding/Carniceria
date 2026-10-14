using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

public enum TipoIdentificacion
{
    [Display(Name = "Cédula nacional")]
    CedulaNacional = 1,

    [Display(Name = "DIMEX")]
    Dimex = 2,

    [Display(Name = "Pasaporte")]
    Pasaporte = 3,

    [Display(Name = "Cédula jurídica")]
    CedulaJuridica = 4
}
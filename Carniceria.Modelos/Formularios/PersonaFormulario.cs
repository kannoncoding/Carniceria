using System;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Formularios;

public abstract class PersonaFormulario
{
    [Display(Name = "Tipo de identificación")]
    [Required(ErrorMessage = "Seleccione un tipo de identificación.")]
    public TipoIdentificacion? TipoIdentificacion { get; set; }

    [Display(Name = "Identificación")]
    [Required(ErrorMessage = "La identificación es obligatoria.")]
    public string Identificacion { get; set; } = string.Empty;

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(
        50,
        MinimumLength = 3,
        ErrorMessage = "El nombre debe tener entre 3 y 50 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "Primer apellido")]
    [Required(ErrorMessage = "El primer apellido es obligatorio.")]
    [StringLength(
        75,
        MinimumLength = 3,
        ErrorMessage = "El primer apellido debe tener entre 3 y 75 caracteres.")]
    public string PrimerApellido { get; set; } = string.Empty;

    [Display(Name = "Segundo apellido")]
    [Required(ErrorMessage = "El segundo apellido es obligatorio.")]
    [StringLength(
        75,
        MinimumLength = 3,
        ErrorMessage = "El segundo apellido debe tener entre 3 y 75 caracteres.")]
    public string SegundoApellido { get; set; } = string.Empty;

    [Display(Name = "Fecha de nacimiento")]
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime? FechaNacimiento { get; set; }
}
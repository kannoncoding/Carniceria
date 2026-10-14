using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Formularios;

public class ProveedorFormulario
{
    [Display(Name = "Tipo de identificación")]
    [Required(ErrorMessage = "Seleccione un tipo de identificación.")]
    public TipoIdentificacion? TipoIdentificacion { get; set; }

    [Display(Name = "Identificación")]
    [Required(ErrorMessage = "La identificación es obligatoria.")]
    public string Identificacion { get; set; } = string.Empty;

    [Display(Name = "Razón social o nombre comercial")]
    [Required(ErrorMessage = "La razón social o nombre comercial es obligatorio.")]
    [StringLength(
        100,
        MinimumLength = 3,
        ErrorMessage =
            "La razón social o nombre comercial debe tener entre 3 y 100 caracteres.")]
    public string RazonSocial { get; set; } = string.Empty;

    [Display(Name = "Nombre de contacto")]
    [Required(ErrorMessage = "El nombre de contacto es obligatorio.")]
    [StringLength(
        100,
        MinimumLength = 3,
        ErrorMessage = "El nombre de contacto debe tener entre 3 y 100 caracteres.")]
    public string NombreContacto { get; set; } = string.Empty;

    [Display(Name = "Teléfono")]
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    public string Telefono { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    public string CorreoElectronico { get; set; } = string.Empty;

    [Display(Name = "Tipo de suministro")]
    [Required(ErrorMessage = "Seleccione un tipo de suministro.")]
    public TipoSuministro? TipoSuministro { get; set; }

    [Display(Name = "Frecuencia de entrega")]
    [Required(ErrorMessage = "Seleccione una frecuencia de entrega.")]
    public FrecuenciaEntrega? FrecuenciaEntrega { get; set; }
}
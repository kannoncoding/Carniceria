using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Formularios;

public class ProductoFormulario
{
    [Display(Name = "Código")]
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(
        20,
        MinimumLength = 1,
        ErrorMessage = "El código debe tener entre 1 y 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(
        80,
        MinimumLength = 3,
        ErrorMessage = "El nombre debe tener entre 3 y 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "Seleccione una categoría.")]
    public CategoriaProducto? Categoria { get; set; }

    [Display(Name = "Unidad de medida")]
    [Required(ErrorMessage = "Seleccione una unidad de medida.")]
    public UnidadMedida? UnidadMedida { get; set; }

    [Display(Name = "Precio por unidad de medida")]
    [Required(ErrorMessage = "El precio por unidad de medida es obligatorio.")]
    public decimal? PrecioUnidadMedida { get; set; }

    [Display(Name = "Existencia actual")]
    [Required(ErrorMessage = "La existencia actual es obligatoria.")]
    public decimal? ExistenciaActual { get; set; }

    [Display(Name = "Existencia mínima")]
    [Required(ErrorMessage = "La existencia mínima es obligatoria.")]
    public decimal? ExistenciaMinima { get; set; }
}
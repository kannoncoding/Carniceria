using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Validacion;

namespace Carniceria.Modelos.Formularios;

public class ProductoFormulario : IValidatableObject
{
    private string _codigo = string.Empty;
    private string _nombre = string.Empty;

    [Display(Name = "Proveedor")]
    public Guid? ProveedorId { get; set; }

    [Display(Name = "Código")]
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(
        20,
        MinimumLength = 1,
        ErrorMessage = "El código debe tener entre 1 y 20 caracteres.")]
    [RegularExpression(
        ReglasTexto.PatronAlfanumerico,
        ErrorMessage = "El código solo puede contener letras y números, sin espacios ni signos.")]
    public string Codigo
    {
        get => _codigo;
        set => _codigo = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(
        80,
        MinimumLength = 3,
        ErrorMessage = "El nombre debe tener entre 3 y 80 caracteres.")]
    [RegularExpression(
        ReglasTexto.PatronAlfanumerico,
        ErrorMessage = "El nombre solo puede contener letras y números, sin espacios ni signos.")]
    public string Nombre
    {
        get => _nombre;
        set => _nombre = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "Seleccione una categoría.")]
    [EnumDataType(
        typeof(CategoriaProducto),
        ErrorMessage = "Seleccione una categoría válida.")]
    public CategoriaProducto? Categoria { get; set; }

    [Display(Name = "Unidad de medida")]
    [Required(ErrorMessage = "Seleccione una unidad de medida.")]
    [EnumDataType(
        typeof(UnidadMedida),
        ErrorMessage = "Seleccione una unidad de medida válida.")]
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

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (PrecioUnidadMedida.HasValue &&
            PrecioUnidadMedida.Value <= 0m)
        {
            yield return new ValidationResult(
                "El precio por unidad de medida debe ser mayor que cero.",
                new[] { nameof(PrecioUnidadMedida) });
        }

        if (ExistenciaActual.HasValue &&
            ExistenciaActual.Value < 0m)
        {
            yield return new ValidationResult(
                "La existencia actual debe ser mayor o igual que cero.",
                new[] { nameof(ExistenciaActual) });
        }

        if (ExistenciaMinima.HasValue &&
            ExistenciaMinima.Value < 0m)
        {
            yield return new ValidationResult(
                "La existencia mínima debe ser mayor o igual que cero.",
                new[] { nameof(ExistenciaMinima) });
        }
    }
}
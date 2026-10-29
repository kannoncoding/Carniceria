using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Catalogos;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Validacion;
using TipoDocumento = Carniceria.Modelos.Enumeraciones.TipoIdentificacion;

namespace Carniceria.Modelos.Formularios;

// Valida datos personales y laborales de un empleado.
public class EmpleadoFormulario : PersonaFormulario
{
    // Normaliza el puesto y la fecha ingresados por el formulario.
    private string _puesto = string.Empty;
    private DateTime? _fechaIngreso;

    // Los empleados pueden usar cédula nacional o DIMEX.
    protected override TipoDocumento[] TiposPermitidos =>
        new[]
        {
            TipoDocumento.CedulaNacional,
            TipoDocumento.Dimex
        };

    // Datos laborales con validación de salario y catálogos.
    [Display(Name = "Salario mensual")]
    [Required(ErrorMessage = "El salario mensual es obligatorio.")]
    [Range(
        typeof(decimal),
        "0",
        "5000000",
        ErrorMessage = "El salario mensual debe estar entre ₡0 y ₡5,000,000.")]
    public decimal? SalarioMensual { get; set; }

    [Display(Name = "Fecha de ingreso")]
    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime? FechaIngreso
    {
        get => _fechaIngreso;
        set => _fechaIngreso = value?.Date;
    }

    [Display(Name = "Área de trabajo")]
    [Required(ErrorMessage = "Seleccione un área de trabajo.")]
    [EnumDataType(
        typeof(AreaTrabajo),
        ErrorMessage = "Seleccione un área de trabajo válida.")]
    public AreaTrabajo? AreaTrabajo { get; set; }

    [Display(Name = "Puesto")]
    [Required(ErrorMessage = "Seleccione un puesto.")]
    public string Puesto
    {
        get => _puesto;
        set => _puesto = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Turno")]
    [Required(ErrorMessage = "Seleccione un turno.")]
    [EnumDataType(
        typeof(Turno),
        ErrorMessage = "Seleccione un turno válido.")]
    public Turno? Turno { get; set; }

    // Verifica que el puesto pertenezca al área elegida.
    public override IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        foreach (var error in base.Validate(validationContext))
        {
            yield return error;
        }

        // Solo se valida la relación cuando ambos valores están presentes.
        if (AreaTrabajo.HasValue &&
            !string.IsNullOrWhiteSpace(Puesto) &&
            !CatalogoPuestos.EsPuestoValido(AreaTrabajo.Value, Puesto))
        {
            yield return new ValidationResult(
                "El puesto seleccionado no pertenece al área de trabajo.",
                new[] { nameof(Puesto) });
        }
    }
}

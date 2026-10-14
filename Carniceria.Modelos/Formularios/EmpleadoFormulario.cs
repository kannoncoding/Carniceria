using System;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Formularios;

public class EmpleadoFormulario : PersonaFormulario
{
    [Display(Name = "Salario mensual")]
    [Required(ErrorMessage = "El salario mensual es obligatorio.")]
    public decimal? SalarioMensual { get; set; }

    [Display(Name = "Fecha de ingreso")]
    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime? FechaIngreso { get; set; }

    [Display(Name = "Área de trabajo")]
    [Required(ErrorMessage = "Seleccione un área de trabajo.")]
    public AreaTrabajo? AreaTrabajo { get; set; }

    [Display(Name = "Puesto")]
    [Required(ErrorMessage = "Seleccione un puesto.")]
    public string Puesto { get; set; } = string.Empty;

    [Display(Name = "Turno")]
    [Required(ErrorMessage = "Seleccione un turno.")]
    public Turno? Turno { get; set; }
}
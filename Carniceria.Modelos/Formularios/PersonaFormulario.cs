using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Validacion;
using TipoDocumento = Carniceria.Modelos.Enumeraciones.TipoIdentificacion;

namespace Carniceria.Modelos.Formularios;

// Define campos y reglas compartidos por formularios de personas.
public abstract class PersonaFormulario : IValidatableObject
{
    // Almacena texto normalizado y fecha sin hora durante el enlace del formulario.
    private string _identificacion = string.Empty;
    private string _nombre = string.Empty;
    private string _primerApellido = string.Empty;
    private string _segundoApellido = string.Empty;
    private DateTime? _fechaNacimiento;

    // Restringe el tipo de identificacion segun la entidad derivada.
    protected abstract TipoDocumento[] TiposPermitidos { get; }

    // Tipo e identificador usados para validar los datos personales.
    [Display(Name = "Tipo de identificación")]
    [Required(ErrorMessage = "Seleccione un tipo de identificación.")]
    [EnumDataType(
        typeof(TipoDocumento),
        ErrorMessage = "Seleccione un tipo de identificación válido.")]
    public TipoDocumento? TipoIdentificacion { get; set; }

    // El valor se recorta antes de aplicar las reglas especificas del documento.
    [Display(Name = "Identificación")]
    [Required(ErrorMessage = "La identificación es obligatoria.")]
    public string Identificacion
    {
        get => _identificacion;
        set => _identificacion = ReglasTexto.Normalizar(value);
    }

    // Nombre y apellidos se recortan antes de validarlos.
    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(
        50,
        MinimumLength = 3,
        ErrorMessage = "El nombre debe tener entre 3 y 50 caracteres.")]
    public string Nombre
    {
        get => _nombre;
        set => _nombre = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Primer apellido")]
    [Required(ErrorMessage = "El primer apellido es obligatorio.")]
    [StringLength(
        75,
        MinimumLength = 3,
        ErrorMessage = "El primer apellido debe tener entre 3 y 75 caracteres.")]
    public string PrimerApellido
    {
        get => _primerApellido;
        set => _primerApellido = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Segundo apellido")]
    [Required(ErrorMessage = "El segundo apellido es obligatorio.")]
    [StringLength(
        75,
        MinimumLength = 3,
        ErrorMessage = "El segundo apellido debe tener entre 3 y 75 caracteres.")]
    public string SegundoApellido
    {
        get => _segundoApellido;
        set => _segundoApellido = ReglasTexto.Normalizar(value);
    }

    // Conserva unicamente el dia indicado en la fecha recibida.
    [Display(Name = "Fecha de nacimiento")]
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime? FechaNacimiento
    {
        get => _fechaNacimiento;
        set => _fechaNacimiento = value?.Date;
    }

    public virtual IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        // Aplica el formato del documento y la lista permitida por el modulo.
        return ReglasIdentificacion.Validar(
            TipoIdentificacion,
            Identificacion,
            TiposPermitidos);
    }
}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Validacion;
using TipoDocumento = Carniceria.Modelos.Enumeraciones.TipoIdentificacion;

namespace Carniceria.Modelos.Formularios;

// Valida identificación, contacto y condiciones de suministro.
public class ProveedorFormulario : IValidatableObject
{
    // Campos de texto normalizados antes de validar o almacenar.
    private string _identificacion = string.Empty;
    private string _razonSocial = string.Empty;
    private string _nombreContacto = string.Empty;
    private string _telefono = string.Empty;
    private string _correoElectronico = string.Empty;

    // Identificación legal y nombre comercial del proveedor.
    [Display(Name = "Tipo de identificación")]
    [Required(ErrorMessage = "Seleccione un tipo de identificación.")]
    [EnumDataType(
        typeof(TipoDocumento),
        ErrorMessage = "Seleccione un tipo de identificación válido.")]
    public TipoDocumento? TipoIdentificacion { get; set; }

    [Display(Name = "Identificación")]
    [Required(ErrorMessage = "La identificación es obligatoria.")]
    public string Identificacion
    {
        get => _identificacion;
        set => _identificacion = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Razón social o nombre comercial")]
    [Required(ErrorMessage = "La razón social o nombre comercial es obligatorio.")]
    [StringLength(
        100,
        MinimumLength = 3,
        ErrorMessage = "La razón social o nombre comercial debe tener entre 3 y 100 caracteres.")]
    [RegularExpression(
        ReglasTexto.PatronAlfanumerico,
        ErrorMessage = "La razón social solo puede contener letras y números, sin espacios ni signos.")]
    public string RazonSocial
    {
        get => _razonSocial;
        set => _razonSocial = ReglasTexto.Normalizar(value);
    }

    // Persona de contacto y medios para comunicarse con el proveedor.
    [Display(Name = "Nombre de contacto")]
    [Required(ErrorMessage = "El nombre de contacto es obligatorio.")]
    [StringLength(
        100,
        MinimumLength = 3,
        ErrorMessage = "El nombre de contacto debe tener entre 3 y 100 caracteres.")]
    public string NombreContacto
    {
        get => _nombreContacto;
        set => _nombreContacto = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Teléfono")]
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [RegularExpression(
        @"\A[0-9]{8}\z",
        ErrorMessage = "El teléfono debe contener exactamente 8 dígitos, sin espacios ni guiones.")]
    public string Telefono
    {
        get => _telefono;
        set => _telefono = ReglasTexto.Normalizar(value);
    }

    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    public string CorreoElectronico
    {
        get => _correoElectronico;
        set => _correoElectronico = ReglasTexto.Normalizar(value);
    }

    // Catálogos de suministro y frecuencia de entrega.
    [Display(Name = "Tipo de suministro")]
    [Required(ErrorMessage = "Seleccione un tipo de suministro.")]
    [EnumDataType(
        typeof(TipoSuministro),
        ErrorMessage = "Seleccione un tipo de suministro válido.")]
    public TipoSuministro? TipoSuministro { get; set; }

    [Display(Name = "Frecuencia de entrega")]
    [Required(ErrorMessage = "Seleccione una frecuencia de entrega.")]
    [EnumDataType(
        typeof(FrecuenciaEntrega),
        ErrorMessage = "Seleccione una frecuencia de entrega válida.")]
    public FrecuenciaEntrega? FrecuenciaEntrega { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        // Verifica el formato según los tipos admitidos para proveedores.
        return ReglasIdentificacion.Validar(
            TipoIdentificacion,
            Identificacion,
            TipoDocumento.CedulaNacional,
            TipoDocumento.CedulaJuridica,
            TipoDocumento.Dimex);
    }
}

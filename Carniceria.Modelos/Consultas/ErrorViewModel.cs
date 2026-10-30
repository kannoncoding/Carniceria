namespace Carniceria.Models;

// Datos de presentacion para la pagina comun de error.
public class ErrorViewModel
{
    // Texto y destino usados por la pagina de error comun.
    public string Titulo { get; init; } =
        "No se pudo completar la solicitud";

    public string Mensaje { get; init; } =
        "Ocurrió un error al procesar la solicitud. " +
        "Puede volver al inicio o elegir un módulo del menú.";

    public string ControladorRegreso { get; init; } = "Home";

    public string TextoRegreso { get; init; } = "Volver al inicio";

    // Identificador de diagnostico opcional de la solicitud actual.
    public string? RequestId { get; set; }

    // Indica si existe un identificador que pueda mostrarse.
    public bool ShowRequestId =>
        !string.IsNullOrEmpty(RequestId);
}

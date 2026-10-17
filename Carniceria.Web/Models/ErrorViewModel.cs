namespace Carniceria.Models;

public class ErrorViewModel
{
    public string Titulo { get; init; } =
        "No se pudo completar la solicitud";

    public string Mensaje { get; init; } =
        "Ocurrió un error al procesar la solicitud. " +
        "Puede volver al inicio o elegir un módulo del menú.";

    public string ControladorRegreso { get; init; } = "Home";

    public string TextoRegreso { get; init; } = "Volver al inicio";

    public string? RequestId { get; set; }

    public bool ShowRequestId =>
        !string.IsNullOrEmpty(RequestId);
}
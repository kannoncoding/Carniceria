namespace Carniceria.Web.Models;

// Presenta el estado de disponibilidad devuelto por la API.
public class ModeloEstadoServicio
{
    // Mensaje asociado al estado consultado.
    public string Mensaje { get; set; } = string.Empty;

    // Indica si la API respondio con el estado disponible.
    public bool Disponible { get; set; }
}

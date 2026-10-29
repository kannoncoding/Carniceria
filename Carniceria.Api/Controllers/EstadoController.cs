using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

// Publica una ruta sencilla para comprobar disponibilidad de la API.
[ApiController]
[Route("api/estado")]
public class EstadoController : ControllerBase
{
    // Informa que la instancia está atendiendo solicitudes.
    [HttpGet]
    public IActionResult ObtenerEstado()
    {
        return Ok(new { Estado = "Disponible" });
    }
}

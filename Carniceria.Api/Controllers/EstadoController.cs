using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/estado")]
public class EstadoController : ControllerBase
{
    [HttpGet]
    public IActionResult ObtenerEstado()
    {
        return Ok(new { Estado = "Disponible" });
    }
}

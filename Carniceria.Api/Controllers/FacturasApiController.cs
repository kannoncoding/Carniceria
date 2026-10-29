using Carniceria.Api.Reglas;
using Carniceria.Modelos.Contratos.Respuestas;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

// Publica los valores centrales que usa el cálculo de facturas.
[ApiController]
[Route("api/facturas")]
public class FacturasApiController : ControllerBase
{
    // Devuelve los porcentajes vigentes como valores decimales JSON.
    [HttpGet("configuracion")]
    public IActionResult ObtenerConfiguracion()
    {
        return Ok(new RespuestaConfiguracionFacturas
        {
            PorcentajeDescuentoMaximo = ReglasFacturacion.PorcentajeDescuentoMaximo,
            PorcentajeIva = ReglasFacturacion.PorcentajeIva
        });
    }
}

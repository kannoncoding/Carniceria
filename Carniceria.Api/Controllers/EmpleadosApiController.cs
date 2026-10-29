using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Mapeos;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/empleados")]
public class EmpleadosApiController : ControllerBase
{
    private readonly IAlmacenCarniceria _almacen;

    public EmpleadosApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var empleados = _almacen.ListarEmpleados()
            .Select(empleado => MapeadorRespuestas.Convertir(empleado))
            .ToArray();

        return Ok(empleados);
    }
}

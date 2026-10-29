using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Api.Mapeos;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

[ApiController]
[Route("api/proveedores")]
public class ProveedoresApiController : ControllerBase
{
    private readonly IAlmacenCarniceria _almacen;

    public ProveedoresApiController(IAlmacenCarniceria almacen)
    {
        _almacen = almacen;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var proveedores = _almacen.ListarProveedores()
            .Select(proveedor => MapeadorRespuestas.Convertir(proveedor))
            .ToArray();

        return Ok(proveedores);
    }
}

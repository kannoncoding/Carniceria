using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Carniceria.Api.Controllers;
using Carniceria.Api.Datos;
using Carniceria.Api.Reglas;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc;

var resultados = new List<(string Nombre, bool Aprobada)>();

// Registra comprobaciones de la ruta y el contrato de configuración.
void Verificar(string nombre, bool condicion)
{
    resultados.Add((nombre, condicion));
}

var almacen = new AlmacenCarniceria();
var accion = new FacturasApiController(almacen).ObtenerConfiguracion();
var respuesta = (accion as OkObjectResult)?.Value as RespuestaConfiguracionFacturas;
var rutaControlador = typeof(FacturasApiController)
    .GetCustomAttribute<RouteAttribute>()?.Template;
var rutaAccion = typeof(FacturasApiController)
    .GetMethod(nameof(FacturasApiController.ObtenerConfiguracion))?
    .GetCustomAttribute<HttpGetAttribute>()?.Template;

Verificar("La configuración publica la ruta estática esperada",
    rutaControlador == "api/facturas" && rutaAccion == "configuracion");
Verificar("Los porcentajes de respuesta tienen tipo decimal",
    typeof(RespuestaConfiguracionFacturas)
        .GetProperties()
        .All(propiedad => propiedad.PropertyType == typeof(decimal)));
Verificar("La respuesta comparte las reglas centrales",
    respuesta?.PorcentajeDescuentoMaximo == ReglasFacturacion.PorcentajeDescuentoMaximo &&
    respuesta?.PorcentajeIva == ReglasFacturacion.PorcentajeIva);

// Confirma que ambos valores se serializan como números, no como texto.
var json = JsonSerializer.Serialize(
    respuesta,
    new JsonSerializerOptions(JsonSerializerDefaults.Web));
using var documento = JsonDocument.Parse(json);
var raiz = documento.RootElement;
Verificar("El JSON conserva ambos porcentajes como números",
    raiz.GetProperty("porcentajeDescuentoMaximo").ValueKind == JsonValueKind.Number &&
    raiz.GetProperty("porcentajeDescuentoMaximo").GetDecimal() == 25m &&
    raiz.GetProperty("porcentajeIva").ValueKind == JsonValueKind.Number &&
    raiz.GetProperty("porcentajeIva").GetDecimal() == 13m);

var validador = typeof(AlmacenCarniceria).GetMethod(
    "ValidarSolicitudFactura",
    BindingFlags.Instance | BindingFlags.NonPublic);
var solicitudBase = new SolicitudFactura
{
    ClienteId = almacen.ListarClientes().First().Id,
    EmpleadoId = almacen.ListarEmpleados().First().Id,
    MetodoPago = MetodoPago.Efectivo,
    Detalles =
    [
        new SolicitudDetalleFactura
        {
            ProductoId = almacen.ListarProductos().First().Id,
            Cantidad = 1m
        }
    ]
};
// Obtiene errores de validación para un descuento específico.
IReadOnlyList<ValidationResult> ObtenerErrores(decimal descuento)
{
    var solicitud = new SolicitudFactura
    {
        ClienteId = solicitudBase.ClienteId,
        EmpleadoId = solicitudBase.EmpleadoId,
        MetodoPago = solicitudBase.MetodoPago,
        PorcentajeDescuento = descuento,
        Detalles = solicitudBase.Detalles
    };
    return (IReadOnlyList<ValidationResult>)validador!.Invoke(almacen, [solicitud])!;
}

Verificar("El validador centraliza el límite sin atributo Range duplicado",
    validador is not null &&
    !typeof(SolicitudFactura).GetProperties().Any(propiedad =>
        propiedad.GetCustomAttributes<RangeAttribute>().Any()) &&
    !typeof(SolicitudDetalleFactura).GetProperties().Any(propiedad =>
        propiedad.GetCustomAttributes<RangeAttribute>().Any()) &&
    ObtenerErrores(ReglasFacturacion.PorcentajeDescuentoMaximo).Count == 0 &&
    ObtenerErrores(ReglasFacturacion.PorcentajeDescuentoMaximo + 0.01m)
        .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.PorcentajeDescuento))));

foreach (var resultado in resultados)
{
    Console.WriteLine($"{(resultado.Aprobada ? "APROBADA" : "FALLÓ")}: {resultado.Nombre}");
}

Console.WriteLine($"Resultado: {resultados.Count(resultado => resultado.Aprobada)}/{resultados.Count} verificaciones.");
return resultados.All(resultado => resultado.Aprobada) ? 0 : 1;

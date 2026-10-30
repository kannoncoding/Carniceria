using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Carniceria.Api.Datos;
using Carniceria.Api.Reglas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;

var resultados = new List<(string Nombre, bool Aprobada)>();

// Registra resultados del contrato de validacion de solicitudes.
void Verificar(string nombre, bool condicion)
{
    resultados.Add((nombre, condicion));
}

var almacen = new AlmacenCarniceria();
var clienteId = almacen.ListarClientes().First().Id;
var empleadoId = almacen.ListarEmpleados().First().Id;
var producto = almacen.ListarProductos().First();
var stockInicial = producto.ExistenciaActual;
var metodoValidacion = typeof(AlmacenCarniceria).GetMethod(
    "ValidarSolicitudFactura",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("No se encontró el validador de facturas.");

// Invoca el validador interno sin publicar una ruta de prueba.
IReadOnlyList<ValidationResult> Validar(SolicitudFactura? solicitud)
{
    return (IReadOnlyList<ValidationResult>)metodoValidacion.Invoke(almacen, [solicitud])!;
}

// Construye una solicitud valida que sirve como base de las variaciones.
SolicitudFactura CrearSolicitudValida(
    Guid? productoSeleccionado = null,
    decimal cantidad = 0.5m,
    decimal descuento = 0m,
    MetodoPago metodo = MetodoPago.Efectivo)
{
    return new SolicitudFactura
    {
        ClienteId = clienteId,
        EmpleadoId = empleadoId,
        MetodoPago = metodo,
        PorcentajeDescuento = descuento,
        Detalles =
        [
            new SolicitudDetalleFactura
            {
                ProductoId = productoSeleccionado ?? producto.Id,
                Cantidad = cantidad
            }
        ]
    };
}

// Comprueba solicitudes validas, limites y campos acumulados.
Verificar("Solicitud nula reporta el campo general", Validar(null)
    .Any(error => error.MemberNames.Contains("Solicitud")));
Verificar("Descuentos cero y máximo son válidos", new[]
{
    CrearSolicitudValida(descuento: 0m),
    CrearSolicitudValida(descuento: ReglasFacturacion.PorcentajeDescuentoMaximo)
}.All(solicitud => Validar(solicitud).Count == 0));
Verificar("Cantidad fraccionaria positiva es válida", Validar(CrearSolicitudValida(cantidad: 0.125m)).Count == 0);
Verificar("Solicitud válida se puede validar repetidamente", Enumerable.Range(0, 3)
    .All(_ => Validar(CrearSolicitudValida()).Count == 0));

var solicitudNula = new SolicitudFactura
{
    ClienteId = clienteId,
    EmpleadoId = empleadoId,
    MetodoPago = MetodoPago.Efectivo,
    Detalles = null!
};
Verificar("Lista de detalles nula reporta el campo", Validar(solicitudNula)
    .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.Detalles))));

var solicitudVacia = CrearSolicitudValida();
solicitudVacia.Detalles.Clear();
Verificar("Lista de detalles vacía reporta el campo", Validar(solicitudVacia)
    .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.Detalles))));

var solicitudLineaNula = new SolicitudFactura
{
    ClienteId = clienteId,
    EmpleadoId = empleadoId,
    MetodoPago = MetodoPago.Efectivo,
    Detalles = [null!]
};
Verificar("Elemento de detalle nulo reporta su índice", Validar(solicitudLineaNula)
    .Any(error => error.MemberNames.Contains("Detalles[0]")));

var solicitudProductoVacio = CrearSolicitudValida(productoSeleccionado: Guid.Empty);
Verificar("Producto vacío reporta la propiedad indexada", Validar(solicitudProductoVacio)
    .Any(error => error.MemberNames.Contains("Detalles[0].ProductoId")));

var solicitudProductoAusente = CrearSolicitudValida(productoSeleccionado: Guid.NewGuid());
Verificar("Producto inexistente reporta la propiedad indexada", Validar(solicitudProductoAusente)
    .Any(error => error.MemberNames.Contains("Detalles[0].ProductoId")));

var solicitudClienteAusente = new SolicitudFactura
{
    ClienteId = Guid.NewGuid(),
    EmpleadoId = empleadoId,
    MetodoPago = MetodoPago.Efectivo,
    Detalles = [new SolicitudDetalleFactura { ProductoId = producto.Id, Cantidad = 1m }]
};
Verificar("Cliente inexistente reporta su propiedad", Validar(solicitudClienteAusente)
    .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.ClienteId))));

var solicitudEmpleadoAusente = new SolicitudFactura
{
    ClienteId = clienteId,
    EmpleadoId = Guid.NewGuid(),
    MetodoPago = MetodoPago.Efectivo,
    Detalles = [new SolicitudDetalleFactura { ProductoId = producto.Id, Cantidad = 1m }]
};
Verificar("Empleado inexistente reporta su propiedad", Validar(solicitudEmpleadoAusente)
    .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.EmpleadoId))));

var solicitudCantidadCero = CrearSolicitudValida(cantidad: 0m);
Verificar("Cantidad cero reporta la propiedad indexada", Validar(solicitudCantidadCero)
    .Any(error => error.MemberNames.Contains("Detalles[0].Cantidad")));

var solicitudCantidadNegativa = CrearSolicitudValida(cantidad: -0.01m);
Verificar("Cantidad negativa reporta la propiedad indexada", Validar(solicitudCantidadNegativa)
    .Any(error => error.MemberNames.Contains("Detalles[0].Cantidad")));

var solicitudDescuentoNegativo = CrearSolicitudValida(descuento: -0.01m);
var solicitudDescuentoSobreMaximo = CrearSolicitudValida(
    descuento: ReglasFacturacion.PorcentajeDescuentoMaximo + 0.01m);
Verificar("Descuentos fuera de límites reportan la propiedad", new[]
{
    solicitudDescuentoNegativo,
    solicitudDescuentoSobreMaximo
}.All(solicitud => Validar(solicitud)
    .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.PorcentajeDescuento)))));

var solicitudMetodoDesconocido = CrearSolicitudValida(metodo: (MetodoPago)999);
Verificar("Valor numérico de método no definido se rechaza", Validar(solicitudMetodoDesconocido)
    .Any(error => error.MemberNames.Contains(nameof(SolicitudFactura.MetodoPago))));

var solicitudRelacionesInvalidas = new SolicitudFactura
{
    ClienteId = Guid.NewGuid(),
    EmpleadoId = Guid.Empty,
    MetodoPago = (MetodoPago)999,
    PorcentajeDescuento = ReglasFacturacion.PorcentajeDescuentoMaximo + 1m,
    Detalles =
    [
        new SolicitudDetalleFactura
        {
            ProductoId = Guid.Empty,
            Cantidad = 0m
        }
    ]
};
var erroresAcumulados = Validar(solicitudRelacionesInvalidas);
Verificar("Relaciones y campos inválidos se acumulan sin detener validación",
    erroresAcumulados.SelectMany(error => error.MemberNames).Distinct().Count() >= 6);
Verificar("La validación no altera las existencias", almacen.ObtenerProducto(producto.Id)!.ExistenciaActual == stockInicial);

foreach (var resultado in resultados)
{
    Console.WriteLine($"{(resultado.Aprobada ? "APROBADA" : "FALLÓ")}: {resultado.Nombre}");
}

Console.WriteLine($"Resultado: {resultados.Count(resultado => resultado.Aprobada)}/{resultados.Count} verificaciones.");
return resultados.All(resultado => resultado.Aprobada) ? 0 : 1;

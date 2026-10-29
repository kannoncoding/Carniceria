using System.Reflection;
using Carniceria.Api.Datos;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

var resultados = new List<(string Nombre, bool Aprobada)>();

// Registra comprobaciones de snapshots y disponibilidad total.
void Verificar(string nombre, bool condicion)
{
    resultados.Add((nombre, condicion));
}

var almacen = new AlmacenCarniceria();
var clienteId = almacen.ListarClientes().First().Id;
var empleadoId = almacen.ListarEmpleados().First().Id;
var res = almacen.ListarProductos().Single(producto => producto.Codigo == "RES001");
var pol = almacen.ListarProductos().Single(producto => producto.Codigo == "POL001");
var productosIniciales = new[] { res, pol };
var metodoPreparacion = typeof(AlmacenCarniceria).GetMethod(
    "PrepararDetallesFactura",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("No se encontró el preparador de líneas.");
var campoSecuencia = typeof(AlmacenCarniceria).GetField(
    "_siguienteNumeroFactura",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("No se encontró la secuencia de facturas.");
var secuenciaInicial = (int)campoSecuencia.GetValue(almacen)!;

// Invoca el preparador interno sin publicar un endpoint auxiliar.
(ResultadoOperacion? Error, IReadOnlyList<DetalleFactura> Detalles) Preparar(
    SolicitudFactura solicitud)
{
    return ((ResultadoOperacion? Error, IReadOnlyList<DetalleFactura> Detalles))
        metodoPreparacion.Invoke(almacen, [solicitud])!;
}

// Crea solicitudes con las relaciones existentes en las semillas.
SolicitudFactura CrearSolicitud(params SolicitudDetalleFactura[] detalles)
{
    return new SolicitudFactura
    {
        ClienteId = clienteId,
        EmpleadoId = empleadoId,
        MetodoPago = MetodoPago.Efectivo,
        Detalles = detalles.ToList()
    };
}

// Crea una línea de solicitud con producto y cantidad.
SolicitudDetalleFactura Linea(Guid productoId, decimal cantidad)
{
    return new SolicitudDetalleFactura
    {
        ProductoId = productoId,
        Cantidad = cantidad
    };
}

// Verifica datos comerciales copiados desde el almacén y no desde la solicitud.
var resultadoLineas = Preparar(CrearSolicitud(Linea(res.Id, 2m), Linea(pol.Id, 1m)));
Verificar("La preparación válida no devuelve error", resultadoLineas.Error is null);
Verificar("Precio y unidad se copian de ambos productos",
    resultadoLineas.Detalles.Count == 2 &&
    resultadoLineas.Detalles[0].PrecioUnitario == 4000m &&
    resultadoLineas.Detalles[0].UnidadMedida == UnidadMedida.Kilogramo &&
    resultadoLineas.Detalles[1].PrecioUnitario == 3000m &&
    resultadoLineas.Detalles[1].UnidadMedida == UnidadMedida.Kilogramo);
Verificar("Las cantidades fraccionarias se conservan",
    Preparar(CrearSolicitud(Linea(res.Id, 0.125m))).Detalles.Single().Cantidad == 0.125m);

// Suma líneas repetidas antes de comparar contra las existencias.
var justoEnExistencia = Preparar(CrearSolicitud(Linea(res.Id, 6m), Linea(res.Id, 4m)));
var sobreExistencia = Preparar(CrearSolicitud(Linea(res.Id, 6m), Linea(res.Id, 5m)));
Verificar("Demanda duplicada que iguala existencias se acepta",
    justoEnExistencia.Error is null && justoEnExistencia.Detalles.Count == 2);
Verificar("Demanda duplicada superior a existencias devuelve conflicto",
    sobreExistencia.Error?.Estado == EstadoOperacion.ConflictoIntegridad &&
    sobreExistencia.Error.Errores.Any(error => error.MemberNames.Contains("Detalles[0].Cantidad")));
Verificar("Conflicto de existencias no devuelve líneas parciales",
    sobreExistencia.Detalles.Count == 0);

// Rechaza la suma decimal desbordada como error de validación controlado.
var demandaDesbordada = Preparar(CrearSolicitud(
    Linea(res.Id, decimal.MaxValue),
    Linea(res.Id, 1m)));
Verificar("Desbordamiento de demanda devuelve validación indexada",
    demandaDesbordada.Error?.Estado == EstadoOperacion.ValidacionFallida &&
    demandaDesbordada.Error.Errores.Any(error => error.MemberNames.Contains("Detalles[1].Cantidad")));

var productoInexistente = Preparar(CrearSolicitud(Linea(Guid.NewGuid(), 1m)));
Verificar("Producto inexistente conserva error de datos 400",
    productoInexistente.Error?.Estado == EstadoOperacion.ValidacionFallida);

// Cambiar un snapshot no altera el producto ni la siguiente preparación.
resultadoLineas.Detalles[0].PrecioUnitario = 1m;
var nuevaPreparacion = Preparar(CrearSolicitud(Linea(res.Id, 1m)));
Verificar("Los snapshots son independientes del almacén",
    nuevaPreparacion.Error is null &&
    nuevaPreparacion.Detalles[0].PrecioUnitario == 4000m &&
    almacen.ObtenerProducto(res.Id)!.PrecioUnidadMedida == 4000m);
Verificar("La preparación aceptada o rechazada no descuenta existencias",
    productosIniciales.All(producto =>
        almacen.ObtenerProducto(producto.Id)!.ExistenciaActual == producto.ExistenciaActual));
Verificar("La preparación no consume la secuencia de factura",
    (int)campoSecuencia.GetValue(almacen)! == secuenciaInicial);

foreach (var resultado in resultados)
{
    Console.WriteLine($"{(resultado.Aprobada ? "APROBADA" : "FALLÓ")}: {resultado.Nombre}");
}

Console.WriteLine($"Resultado: {resultados.Count(resultado => resultado.Aprobada)}/{resultados.Count} verificaciones.");
return resultados.All(resultado => resultado.Aprobada) ? 0 : 1;

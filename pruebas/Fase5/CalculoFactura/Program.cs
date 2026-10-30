using System.Reflection;
using Carniceria.Api.Datos;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

var resultados = new List<(string Nombre, bool Aprobada)>();

// Registra comprobaciones del calculo decimal y de sus limites.
void Verificar(string nombre, bool condicion)
{
    resultados.Add((nombre, condicion));
}

var almacen = new AlmacenCarniceria();
var metodoCalculo = typeof(AlmacenCarniceria).GetMethod(
    "CalcularImportesFactura",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("No se encontró el calculador de facturas.");

// Invoca el calculo interno sin exponerlo mediante una ruta HTTP.
(ResultadoOperacion? Error, Factura? Factura) Calcular(Factura candidata)
{
    return ((ResultadoOperacion? Error, Factura? Factura))
        metodoCalculo.Invoke(almacen, [candidata])!;
}

// Construye un candidato con lineas recibidas desde el almacen.
Factura CrearFactura(decimal descuento, params DetalleFactura[] detalles)
{
    return new Factura
    {
        PorcentajeDescuento = descuento,
        Detalles = detalles.ToList()
    };
}

DetalleFactura Linea(decimal cantidad, decimal precio)
{
    return new DetalleFactura
    {
        Cantidad = cantidad,
        PrecioUnitario = precio,
        SubtotalLinea = 777m
    };
}

// Comprueba el ejemplo academico y los extremos permitidos del descuento.
var ejemplo = CrearFactura(10m, Linea(2m, 4000m), Linea(1m, 3000m));
var calculoEjemplo = Calcular(ejemplo);
Verificar("El ejemplo 10 % produce importes esperados",
    calculoEjemplo.Error is null &&
    calculoEjemplo.Factura!.Subtotal == 11000m &&
    calculoEjemplo.Factura.MontoDescuento == 1100m &&
    calculoEjemplo.Factura.BaseGravable == 9900m &&
    calculoEjemplo.Factura.MontoIva == 1287m &&
    calculoEjemplo.Factura.Total == 11187m);

var sinDescuento = Calcular(CrearFactura(0m, Linea(2m, 4000m)));
var descuentoMaximo = Calcular(CrearFactura(25m, Linea(2m, 4000m)));
Verificar("Descuento cero calcula el impuesto sobre el subtotal",
    sinDescuento.Error is null &&
    sinDescuento.Factura!.Subtotal == 8000m &&
    sinDescuento.Factura.MontoDescuento == 0m &&
    sinDescuento.Factura.MontoIva == 1040m &&
    sinDescuento.Factura.Total == 9040m);
Verificar("Descuento máximo calcula base, IVA y total",
    descuentoMaximo.Error is null &&
    descuentoMaximo.Factura!.MontoDescuento == 2000m &&
    descuentoMaximo.Factura.BaseGravable == 6000m &&
    descuentoMaximo.Factura.MontoIva == 780m &&
    descuentoMaximo.Factura.Total == 6780m);

// Redondea cada linea antes de sumarla y admite importes finales en cero.
var puntoMedio = Calcular(CrearFactura(0m, Linea(0.005m, 1m), Linea(0.005m, 1m)));
var importeCero = Calcular(CrearFactura(0m, Linea(0.001m, 1m)));
Verificar("Los puntos medios redondean cada línea AwayFromZero antes del subtotal",
    puntoMedio.Error is null &&
    puntoMedio.Factura!.Detalles.All(detalle => detalle.SubtotalLinea == 0.01m) &&
    puntoMedio.Factura.Subtotal == 0.02m);
Verificar("Un importe redondeado a cero no se rechaza",
    importeCero.Error is null &&
    importeCero.Factura!.Subtotal == 0m &&
    importeCero.Factura.Total == 0m);

// El calculo trabaja en copias y conserva el candidato original.
Verificar("El resultado y sus líneas son copias independientes",
    calculoEjemplo.Error is null &&
    !ReferenceEquals(ejemplo, calculoEjemplo.Factura) &&
    !ReferenceEquals(ejemplo.Detalles[0], calculoEjemplo.Factura!.Detalles[0]));
Verificar("El cálculo no modifica los importes originales",
    ejemplo.Subtotal == 0m &&
    ejemplo.Detalles.All(detalle => detalle.SubtotalLinea == 777m));

var desbordamientoLinea = Calcular(CrearFactura(0m, Linea(decimal.MaxValue, 2m)));
Verificar("Desbordamiento de línea devuelve error controlado e indexado",
    desbordamientoLinea.Error?.Estado == EstadoOperacion.ValidacionFallida &&
    desbordamientoLinea.Error.Errores.Any(error => error.MemberNames.Contains("Detalles[0].Cantidad")));

var desbordamientoSubtotal = Calcular(CrearFactura(
    0m,
    Linea(decimal.MaxValue, 1m),
    Linea(decimal.MaxValue, 1m)));
Verificar("Desbordamiento del subtotal devuelve error controlado",
    desbordamientoSubtotal.Error?.Estado == EstadoOperacion.ValidacionFallida &&
    desbordamientoSubtotal.Error.Errores.Any(error => error.MemberNames.Contains("Detalles[1].Cantidad")));

var descuentoRepresentable = Calcular(CrearFactura(
    25m,
    Linea(30000000000000000000000000000m, 1m)));
Verificar("Descuento en subtotal alto conserva sus importes representables",
    descuentoRepresentable.Error is null &&
    descuentoRepresentable.Factura!.MontoDescuento == 7500000000000000000000000000m &&
    descuentoRepresentable.Factura.BaseGravable == 22500000000000000000000000000m &&
    descuentoRepresentable.Factura.MontoIva == 2925000000000000000000000000m &&
    descuentoRepresentable.Factura.Total == 25425000000000000000000000000m);

var descuentoDiminuto = Calcular(CrearFactura(
    0.0000000000000000000000000001m,
    Linea(30000000000000000000000000000m, 1m)));
Verificar("Descuento decimal diminuto conserva su importe redondeado",
    descuentoDiminuto.Error is null &&
    descuentoDiminuto.Factura!.MontoDescuento == 0.03m);

var desbordamientoTotal = Calcular(CrearFactura(0m, Linea(decimal.MaxValue, 1m)));
Verificar("Desbordamiento del total devuelve error controlado",
    desbordamientoTotal.Error?.Estado == EstadoOperacion.ValidacionFallida &&
    desbordamientoTotal.Error.Errores.Any(error => error.MemberNames.Contains(nameof(Factura.Total))));

foreach (var resultado in resultados)
{
    Console.WriteLine($"{(resultado.Aprobada ? "APROBADA" : "FALLÓ")}: {resultado.Nombre}");
}

Console.WriteLine($"Resultado: {resultados.Count(resultado => resultado.Aprobada)}/{resultados.Count} verificaciones.");
return resultados.All(resultado => resultado.Aprobada) ? 0 : 1;

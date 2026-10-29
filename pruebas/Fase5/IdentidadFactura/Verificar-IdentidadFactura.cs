using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Carniceria.Api.Datos;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;

const int cantidadFacturas = 32;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-CR");
var resultados = new List<(string Nombre, bool Aprobada)>();

// Registra condiciones del generador sin acceder a rutas HTTP.
void Verificar(string nombre, bool condicion)
{
    resultados.Add((nombre, condicion));
}

var almacen = new AlmacenCarniceria();
var metodoPreparacion = typeof(AlmacenCarniceria).GetMethod(
    "PrepararFacturaValidadaParaGuardar",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("No se encontró el preparador interno de facturas.");
var pares = new ConcurrentBag<(Factura Candidata, Factura Preparada)>();
var inicioEmision = DateTimeOffset.UtcNow.AddSeconds(-1);

// Genera candidatos simultáneos con datos de servidor suplantados.
Parallel.For(0, cantidadFacturas, indice =>
{
    var candidata = new Factura
    {
        Numero = "SUPLANTADO",
        FechaEmision = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Estado = EstadoFactura.Anulada,
        Detalles =
        [
            new DetalleFactura
            {
                ProductoId = Guid.NewGuid(),
                UnidadMedida = UnidadMedida.Kilogramo,
                Cantidad = indice + 0.5m,
                PrecioUnitario = 1000m,
                SubtotalLinea = 1000m
            }
        ]
    };

    var preparada = (Factura)metodoPreparacion.Invoke(almacen, [candidata])!;
    pares.Add((candidata, preparada));
});
var finEmision = DateTimeOffset.UtcNow.AddSeconds(1);

var facturas = pares.Select(par => par.Preparada).ToArray();
var numeros = facturas
    .Select(factura => factura.Numero)
    .OrderBy(numero => numero, StringComparer.Ordinal)
    .ToArray();
var numerosEsperados = Enumerable.Range(1, cantidadFacturas)
    .Select(secuencia => "FAC" + secuencia.ToString("D6", CultureInfo.InvariantCulture))
    .ToArray();
Verificar("Números FAC únicos y secuenciales bajo concurrencia", numeros.SequenceEqual(numerosEsperados));
Verificar("Estado Pendiente impuesto por API", facturas.All(factura => factura.Estado == EstadoFactura.Pendiente));
Verificar("Fecha con offset de Costa Rica", facturas.All(factura => factura.FechaEmision.Offset == TimeSpan.FromHours(-6)));
Verificar("Fecha de emisión dentro del intervalo de ejecución", facturas.All(factura =>
    factura.FechaEmision >= inicioEmision && factura.FechaEmision <= finEmision));
Verificar("Id de factura estable desde candidato de API", pares.All(par => par.Candidata.Id == par.Preparada.Id));
Verificar("Id nuevo y único por detalle", facturas.SelectMany(factura => factura.Detalles).All(detalle => detalle.Id != Guid.Empty) &&
    facturas.SelectMany(factura => factura.Detalles).Select(detalle => detalle.Id).Distinct().Count() == cantidadFacturas &&
    pares.All(par => par.Candidata.Detalles[0].Id != par.Preparada.Detalles[0].Id));
Verificar("Identidad y estado suplantados no mutan el candidato", pares.All(par =>
    par.Candidata.Numero == "SUPLANTADO" &&
    par.Candidata.FechaEmision == new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero) &&
    par.Candidata.Estado == EstadoFactura.Anulada));
Verificar("El candidato conserva sus detalles originales", pares.All(par =>
    par.Candidata.Detalles.Count == 1 && par.Candidata.Detalles[0].Id != Guid.Empty));

foreach (var resultado in resultados)
{
    Console.WriteLine($"{(resultado.Aprobada ? "APROBADA" : "FALLÓ")}: {resultado.Nombre}");
}

Console.WriteLine($"Resultado: {resultados.Count(resultado => resultado.Aprobada)}/{resultados.Count} verificaciones.");
return resultados.All(resultado => resultado.Aprobada) ? 0 : 1;

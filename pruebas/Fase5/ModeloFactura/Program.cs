using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;

// Acumula verificaciones semanticas del modelo de factura.
var verificaciones = new List<(string Nombre, bool Aprobada)>();

// Registra las condiciones del modelo y conserva el resultado total.
void Verificar(string nombre, bool condicion)
{
    verificaciones.Add((nombre, condicion));
}

// Construye un ejemplo maestro-detalle con cantidad fraccionaria.
var factura = new Factura
{
    Numero = "FAC000001",
    ClienteId = Guid.NewGuid(),
    EmpleadoId = Guid.NewGuid(),
    MetodoPago = MetodoPago.SinpeMovil,
    Estado = EstadoFactura.Pendiente,
    Detalles =
    [
        new DetalleFactura
        {
            ProductoId = Guid.NewGuid(),
            UnidadMedida = UnidadMedida.Kilogramo,
            Cantidad = 1.25m,
            PrecioUnitario = 4000m,
            SubtotalLinea = 5000m
        }
    ]
};

// Copia la factura y verifica que conserve identidad sin compartir objetos.
var copia = factura.Copiar();
Verificar("Identificadores de factura no vacíos y estables", factura.Id != Guid.Empty && copia.Id == factura.Id);
Verificar("Colección de detalles independiente", !ReferenceEquals(factura.Detalles, copia.Detalles));
Verificar("Detalle copiado como objeto independiente", !ReferenceEquals(factura.Detalles[0], copia.Detalles[0]));
Verificar("Identidad de detalle conservada al copiar", copia.Detalles[0].Id == factura.Detalles[0].Id);
copia.Detalles[0].Cantidad = 9m;
copia.Detalles.Add(new DetalleFactura { ProductoId = Guid.NewGuid(), Cantidad = 2m });
Verificar("Cambios en copia no alteran líneas originales", factura.Detalles.Count == 1 && factura.Detalles[0].Cantidad == 1.25m);

// Verifica que la solicitud solo declare los campos editables acordados.
string[] propiedadesSolicitud = typeof(SolicitudFactura)
    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
    .Select(propiedad => propiedad.Name)
    .OrderBy(nombre => nombre, StringComparer.Ordinal)
    .ToArray();
string[] propiedadesEsperadas = ["ClienteId", "Detalles", "EmpleadoId", "MetodoPago", "PorcentajeDescuento"];
Verificar("Solicitud de factura sin campos calculados ni identificadores de servidor", propiedadesSolicitud.SequenceEqual(propiedadesEsperadas));

string[] propiedadesLinea = typeof(SolicitudDetalleFactura)
    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
    .Select(propiedad => propiedad.Name)
    .OrderBy(nombre => nombre, StringComparer.Ordinal)
    .ToArray();
Verificar("Solicitud de detalle contiene solo producto y cantidad", propiedadesLinea.SequenceEqual(["Cantidad", "ProductoId"]));

// Confirma los valores y la etiqueta de los catalogos de factura.
Verificar("Catálogo de métodos de pago", Enum.GetValues<MetodoPago>().SequenceEqual(
    [MetodoPago.Efectivo, MetodoPago.Tarjeta, MetodoPago.SinpeMovil, MetodoPago.Transferencia]));
var etiquetaSinpe = typeof(MetodoPago)
    .GetField(nameof(MetodoPago.SinpeMovil))?
    .GetCustomAttribute<DisplayAttribute>()?
    .Name;
Verificar("Etiqueta SINPE Móvil", etiquetaSinpe == "SINPE Móvil");
Verificar("Catálogo de estados de factura", Enum.GetValues<EstadoFactura>().SequenceEqual(
    [EstadoFactura.Pendiente, EstadoFactura.Pagada, EstadoFactura.Anulada]));

foreach (var verificacion in verificaciones)
{
    Console.WriteLine($"{(verificacion.Aprobada ? "APROBADA" : "FALLÓ")}: {verificacion.Nombre}");
}

Console.WriteLine($"Resultado: {verificaciones.Count(verificacion => verificacion.Aprobada)}/{verificaciones.Count} verificaciones.");
return verificaciones.All(verificacion => verificacion.Aprobada) ? 0 : 1;

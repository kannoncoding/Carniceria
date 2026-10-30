using Carniceria.Api.Reglas;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

var verificaciones = new List<(string Nombre, bool Aprobada)>();

// Guarda el resultado de cada caso de la matriz de transiciones.
void Verificar(string nombre, bool aprobada)
{
    verificaciones.Add((nombre, aprobada));
}

var matriz = new (EstadoFactura Estado, OperacionFactura Operacion, bool Permitida)[]
{
    (EstadoFactura.Pendiente, OperacionFactura.Editar, true),
    (EstadoFactura.Pendiente, OperacionFactura.Pagar, true),
    (EstadoFactura.Pendiente, OperacionFactura.Anular, false),
    (EstadoFactura.Pendiente, OperacionFactura.Eliminar, true),
    (EstadoFactura.Pagada, OperacionFactura.Editar, true),
    (EstadoFactura.Pagada, OperacionFactura.Pagar, false),
    (EstadoFactura.Pagada, OperacionFactura.Anular, true),
    (EstadoFactura.Pagada, OperacionFactura.Eliminar, false),
    (EstadoFactura.Anulada, OperacionFactura.Editar, false),
    (EstadoFactura.Anulada, OperacionFactura.Pagar, false),
    (EstadoFactura.Anulada, OperacionFactura.Anular, false),
    (EstadoFactura.Anulada, OperacionFactura.Eliminar, false)
};

// Comprueba la matriz sin cambiar factura ni existencias.
foreach (var caso in matriz)
{
    var factura = new Factura { Estado = caso.Estado };
    var producto = new Producto { ExistenciaActual = 12m };
    var rechazo = ReglasTransicionFactura.ValidarTransicion(
        factura.Estado,
        caso.Operacion);
    var mensajeEsperado = caso.Operacion switch
    {
        OperacionFactura.Editar when caso.Estado == EstadoFactura.Anulada =>
            "Una factura anulada no se puede editar.",
        OperacionFactura.Editar =>
            "El estado actual de la factura no permite editarla.",
        OperacionFactura.Pagar =>
            "Solo se pueden pagar facturas pendientes.",
        OperacionFactura.Anular =>
            "Solo se pueden anular facturas pagadas.",
        OperacionFactura.Eliminar =>
            "Solo se pueden eliminar facturas pendientes.",
        _ => string.Empty
    };
    var resultadoEsperado = caso.Permitida
        ? rechazo is null
        : rechazo?.Estado == EstadoOperacion.ConflictoIntegridad &&
          rechazo.Mensaje == mensajeEsperado &&
          rechazo.Errores.Any(error =>
              error.MemberNames.Contains(nameof(Factura.Estado)));

    Verificar(
        $"{caso.Estado}/{caso.Operacion}: decision de la matriz",
        resultadoEsperado);
    Verificar(
        $"{caso.Estado}/{caso.Operacion}: datos sin cambios",
        factura.Estado == caso.Estado && producto.ExistenciaActual == 12m);
}

// Rechaza valores numericos que no pertenecen a los enums del ciclo.
var estadoDesconocido = ReglasTransicionFactura.ValidarTransicion(
    (EstadoFactura)99,
    OperacionFactura.Pagar);
Verificar(
    "Estado no definido devuelve conflicto",
    estadoDesconocido?.Estado == EstadoOperacion.ConflictoIntegridad &&
    estadoDesconocido.Mensaje == "El estado actual de la factura no está definido." &&
    estadoDesconocido.Errores.Any(error =>
        error.MemberNames.Contains(nameof(Factura.Estado))));

var operacionDesconocida = ReglasTransicionFactura.ValidarTransicion(
    EstadoFactura.Pendiente,
    (OperacionFactura)99);
Verificar(
    "Operacion no definida devuelve conflicto",
    operacionDesconocida?.Estado == EstadoOperacion.ConflictoIntegridad &&
    operacionDesconocida.Mensaje == "La operación solicitada no está definida." &&
    operacionDesconocida.Errores.Any(error =>
        error.MemberNames.Contains(nameof(OperacionFactura))));

foreach (var verificacion in verificaciones)
{
    Console.WriteLine($"{(verificacion.Aprobada ? "APROBADA" : "FALLIDA")}: {verificacion.Nombre}");
}

var fallidas = verificaciones.Count(verificacion => !verificacion.Aprobada);
Console.WriteLine($"Resultado: {verificaciones.Count - fallidas}/{verificaciones.Count}");
return fallidas == 0 ? 0 : 1;

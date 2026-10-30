using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Reglas;

// Centraliza las transiciones permitidas para las facturas.
public static class ReglasTransicionFactura
{
    // Devuelve un conflicto cuando la operacion no corresponde al estado actual.
    public static ResultadoOperacion? ValidarTransicion(
        EstadoFactura estadoActual,
        OperacionFactura operacion)
    {
        if (!Enum.IsDefined(estadoActual))
        {
            return ResultadoOperacion.ConflictoIntegridad(
                nameof(Factura.Estado),
                "El estado actual de la factura no está definido.");
        }

        if (!Enum.IsDefined(operacion))
        {
            return ResultadoOperacion.ConflictoIntegridad(
                nameof(OperacionFactura),
                "La operación solicitada no está definida.");
        }

        if (EsTransicionPermitida(estadoActual, operacion))
        {
            return null;
        }

        return ResultadoOperacion.ConflictoIntegridad(
            nameof(Factura.Estado),
            ObtenerMensajeRechazo(estadoActual, operacion));
    }

    // Acepta solo las transiciones descritas para cada estado.
    private static bool EsTransicionPermitida(
        EstadoFactura estadoActual,
        OperacionFactura operacion)
    {
        return (estadoActual, operacion) switch
        {
            (EstadoFactura.Pendiente, OperacionFactura.Editar) => true,
            (EstadoFactura.Pendiente, OperacionFactura.Pagar) => true,
            (EstadoFactura.Pendiente, OperacionFactura.Eliminar) => true,
            (EstadoFactura.Pagada, OperacionFactura.Editar) => true,
            (EstadoFactura.Pagada, OperacionFactura.Anular) => true,
            _ => false
        };
    }

    // Explica por que la operacion no se admite en el estado indicado.
    private static string ObtenerMensajeRechazo(
        EstadoFactura estadoActual,
        OperacionFactura operacion)
    {
        return operacion switch
        {
            OperacionFactura.Editar when estadoActual == EstadoFactura.Anulada =>
                "Una factura anulada no se puede editar.",
            OperacionFactura.Editar =>
                "El estado actual de la factura no permite editarla.",
            OperacionFactura.Pagar =>
                "Solo se pueden pagar facturas pendientes.",
            OperacionFactura.Anular =>
                "Solo se pueden anular facturas pagadas.",
            OperacionFactura.Eliminar =>
                "Solo se pueden eliminar facturas pendientes.",
            _ => "La operación no está permitida para el estado actual de la factura."
        };
    }
}

namespace Carniceria.Api.Reglas;

// Identifica las operaciones que cambian el ciclo de una factura.
public enum OperacionFactura
{
    Editar = 1,
    Pagar = 2,
    Anular = 3,
    Eliminar = 4
}

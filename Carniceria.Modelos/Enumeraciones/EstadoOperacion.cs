namespace Carniceria.Modelos.Enumeraciones;

// Resultados posibles de una operación del almacén.
public enum EstadoOperacion
{
    Exito = 1,
    ValidacionFallida = 2,
    Duplicado = 3,
    NoEncontrado = 4,
    ConflictoIntegridad = 5
}

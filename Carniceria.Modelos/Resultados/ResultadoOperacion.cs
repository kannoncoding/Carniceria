using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Resultados;

// Resume el estado, identificador y errores de una operación.
public sealed class ResultadoOperacion
{
    // Estado uniforme devuelto por las operaciones del almacén.
    public EstadoOperacion Estado { get; }

    // Resume si la operación terminó correctamente.
    public bool EsExitoso => Estado == EstadoOperacion.Exito;

    // Identificador de la entidad cuando la operación lo produce.
    public Guid? Id { get; }

    // Mensaje general y errores de validación asociados al resultado.
    public string Mensaje { get; }

    public IReadOnlyList<ValidationResult> Errores { get; }

    private ResultadoOperacion(
        EstadoOperacion estado,
        Guid? id,
        string mensaje,
        IEnumerable<ValidationResult> errores)
    {
        // Copia la colección para impedir cambios desde el origen.
        Estado = estado;
        Id = id;
        Mensaje = mensaje;
        Errores = Array.AsReadOnly(errores.ToArray());
    }

    public static ResultadoOperacion Correcto(
        Guid id,
        string mensaje)
    {
        // Crea un resultado exitoso con el identificador de la entidad.
        return new ResultadoOperacion(
            EstadoOperacion.Exito,
            id,
            mensaje,
            Array.Empty<ValidationResult>());
    }

    public static ResultadoOperacion ValidacionFallida(
        IEnumerable<ValidationResult> errores)
    {
        // Conserva errores de campos sin asignar un identificador.
        return new ResultadoOperacion(
            EstadoOperacion.ValidacionFallida,
            null,
            "Revise los datos ingresados.",
            errores);
    }

    public static ResultadoOperacion Duplicado(
        string campo,
        string mensaje)
    {
        // Asocia el conflicto de duplicado con el campo responsable.
        return new ResultadoOperacion(
            EstadoOperacion.Duplicado,
            null,
            mensaje,
            new[]
            {
                new ValidationResult(
                    mensaje,
                    new[] { campo })
            });
    }

    public static ResultadoOperacion NoEncontrado(
        string mensaje)
    {
        // Representa una consulta o modificación sobre un Id ausente.
        return new ResultadoOperacion(
            EstadoOperacion.NoEncontrado,
            null,
            mensaje,
            Array.Empty<ValidationResult>());
    }

    public static ResultadoOperacion ConflictoIntegridad(
        string campo,
        string mensaje)
    {
        // Representa un conflicto con las relaciones o el estado actual de los datos.
        return new ResultadoOperacion(
            EstadoOperacion.ConflictoIntegridad,
            null,
            mensaje,
            new[]
            {
                new ValidationResult(
                    mensaje,
                    new[] { campo })
            });
    }
}

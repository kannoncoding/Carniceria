using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Resultados;

public sealed class ResultadoOperacion
{
    public EstadoOperacion Estado { get; }

    public bool EsExitoso => Estado == EstadoOperacion.Exito;

    public Guid? Id { get; }

    public string Mensaje { get; }

    public IReadOnlyList<ValidationResult> Errores { get; }

    private ResultadoOperacion(
        EstadoOperacion estado,
        Guid? id,
        string mensaje,
        IEnumerable<ValidationResult> errores)
    {
        Estado = estado;
        Id = id;
        Mensaje = mensaje;
        Errores = Array.AsReadOnly(errores.ToArray());
    }

    public static ResultadoOperacion Correcto(
        Guid id,
        string mensaje)
    {
        return new ResultadoOperacion(
            EstadoOperacion.Exito,
            id,
            mensaje,
            Array.Empty<ValidationResult>());
    }

    public static ResultadoOperacion ValidacionFallida(
        IEnumerable<ValidationResult> errores)
    {
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

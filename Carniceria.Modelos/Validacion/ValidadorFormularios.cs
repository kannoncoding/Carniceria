using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Validacion;

// Ejecuta la validacion de formularios mediante DataAnnotations.
public static class ValidadorFormularios
{
    // Ejecuta atributos y validaciones propias del formulario.
    public static IReadOnlyList<ValidationResult> Validar(object formulario)
    {
        ArgumentNullException.ThrowIfNull(formulario);

        // Recopila los errores detectados por la validacion.
        var errores = new List<ValidationResult>();

        // El contexto evalua reglas que dependen del formulario completo.
        var contexto = new ValidationContext(formulario);

        Validator.TryValidateObject(
            formulario,
            contexto,
            errores,
            validateAllProperties: true);

        // Impide que el consumidor modifique la coleccion devuelta.
        return errores.AsReadOnly();
    }
}

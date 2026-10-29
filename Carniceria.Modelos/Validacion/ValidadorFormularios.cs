using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Validacion;

// Ejecuta la validación de formularios mediante DataAnnotations.
public static class ValidadorFormularios
{
    // Ejecuta atributos y validaciones propias del formulario.
    public static IReadOnlyList<ValidationResult> Validar(object formulario)
    {
        ArgumentNullException.ThrowIfNull(formulario);

        // Recopila los errores detectados por la validación.
        var errores = new List<ValidationResult>();

        // El contexto evalúa reglas que dependen del formulario completo.
        var contexto = new ValidationContext(formulario);

        Validator.TryValidateObject(
            formulario,
            contexto,
            errores,
            validateAllProperties: true);

        // Impide que el consumidor modifique la colección devuelta.
        return errores.AsReadOnly();
    }
}

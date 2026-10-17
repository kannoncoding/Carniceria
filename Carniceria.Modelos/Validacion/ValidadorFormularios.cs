using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Validacion;

public static class ValidadorFormularios
{
    public static IReadOnlyList<ValidationResult> Validar(object formulario)
    {
        ArgumentNullException.ThrowIfNull(formulario);

        var errores = new List<ValidationResult>();

        var contexto = new ValidationContext(formulario);

        Validator.TryValidateObject(
            formulario,
            contexto,
            errores,
            validateAllProperties: true);

        return errores.AsReadOnly();
    }
}
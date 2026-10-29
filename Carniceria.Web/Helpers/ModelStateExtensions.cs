using System;
using System.Linq;
using Carniceria.Modelos.Resultados;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Carniceria.Web.Helpers;

// Integra los errores del almacén con los mensajes del formulario MVC.
public static class ModelStateExtensions
{
    // Asocia cada error de dominio al campo correspondiente del formulario.
    public static void AgregarErrores(
        this ModelStateDictionary modelState,
        ResultadoOperacion resultado)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(resultado);

        if (resultado.EsExitoso)
        {
            return;
        }

        // Un error sin campos asociados aparece en el resumen general.
        if (resultado.Errores.Count == 0)
        {
            modelState.AddModelError(
                string.Empty,
                resultado.Mensaje);

            return;
        }

        foreach (var error in resultado.Errores)
        {
            string mensaje = string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? resultado.Mensaje
                : error.ErrorMessage;

            var campos = error.MemberNames
                .Where(campo => !string.IsNullOrWhiteSpace(campo))
                .Distinct(StringComparer.Ordinal)
                .DefaultIfEmpty(string.Empty);

            foreach (string campo in campos)
            {
                modelState.AddModelError(campo, mensaje);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Validacion;

// Centraliza los formatos de identificacion aceptados.
public static class ReglasIdentificacion
{
    // Aplica el formato propio de cada documento y la lista del modulo.
    public static IEnumerable<ValidationResult> Validar(
        TipoIdentificacion? tipo,
        string? identificacion,
        params TipoIdentificacion[] tiposPermitidos)
    {
        // El atributo Required informa si falta seleccionar el tipo.
        if (!tipo.HasValue)
        {
            yield break;
        }

        if (Array.IndexOf(tiposPermitidos, tipo.Value) < 0)
        {
            yield return new ValidationResult(
                "El tipo de identificación no está permitido en este módulo.",
                new[] { "TipoIdentificacion" });

            yield break;
        }

        string valor = ReglasTexto.Normalizar(identificacion);

        // El atributo Required informa si falta el numero de identificacion.
        if (valor.Length == 0)
        {
            yield break;
        }

        string patron;
        string mensaje;

        switch (tipo.Value)
        {
            case TipoIdentificacion.CedulaNacional:
                patron = @"\A[0-9]-[0-9]{4}-[0-9]{4}\z";
                mensaje = "La cédula debe tener el formato 1-1111-0909.";
                break;

            case TipoIdentificacion.Dimex:
                patron = @"\A[0-9]{12}\z";
                mensaje = "El DIMEX debe contener exactamente 12 dígitos.";
                break;

            case TipoIdentificacion.Pasaporte:
                patron = ReglasTexto.PatronAlfanumerico;
                mensaje =
                    "El pasaporte debe tener entre 1 y 50 caracteres alfanuméricos, sin espacios ni signos.";

                if (valor.Length > 50)
                {
                    yield return new ValidationResult(
                        mensaje,
                        new[] { "Identificacion" });

                    yield break;
                }

                break;

            case TipoIdentificacion.CedulaJuridica:
                patron = @"\A3-[0-9]{3}-[0-9]{6}\z";
                mensaje =
                    "La cédula jurídica debe tener el formato 3-101-123456.";
                break;

            default:
                yield return new ValidationResult(
                    "Seleccione un tipo de identificación válido.",
                    new[] { "TipoIdentificacion" });

                yield break;
        }

        if (!Regex.IsMatch(valor, patron))
        {
            yield return new ValidationResult(
                mensaje,
                new[] { "Identificacion" });
        }
    }
}

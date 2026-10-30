using System;
using System.Collections.Generic;
using Carniceria.Api.Mapeos;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Resultados;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Api.Controllers;

// Comparte la conversion de resultados y errores entre los controladores API.
public abstract class ControladorApiBase : ControllerBase
{
    // Traduce el estado del almacen al codigo HTTP correspondiente.
    protected IActionResult ConvertirResultadoFallido(
        ResultadoOperacion resultado)
    {
        var error = MapeadorErroresApi.Convertir(resultado);

        return resultado.Estado switch
        {
            Carniceria.Modelos.Enumeraciones.EstadoOperacion.Duplicado =>
                Conflict(error),
            Carniceria.Modelos.Enumeraciones.EstadoOperacion.ConflictoIntegridad =>
                Conflict(error),
            Carniceria.Modelos.Enumeraciones.EstadoOperacion.NoEncontrado =>
                NotFound(error),
            _ => BadRequest(error)
        };
    }

    // Genera un ErrorApi para rutas que no encuentran su recurso.
    protected IActionResult CrearNoEncontrado(string mensaje)
    {
        return NotFound(new ErrorApi
        {
            Codigo = "NoEncontrado",
            Mensaje = mensaje
        });
    }

    // Devuelve un error de campo cuando el criterio no es admitido.
    protected IActionResult CrearCriterioInvalido(string nombreCriterio)
    {
        return BadRequest(new ErrorApi
        {
            Codigo = "CriterioInvalido",
            Mensaje = "Seleccione un criterio de búsqueda válido.",
            ErroresPorCampo = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [nombreCriterio] = new[] { "El criterio de búsqueda no es válido." }
            }
        });
    }
}

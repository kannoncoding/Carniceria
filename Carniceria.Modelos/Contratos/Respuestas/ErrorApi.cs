using System.Collections.Generic;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Forma uniforme de comunicar errores de la API.
public sealed class ErrorApi
{
    // Codigo estable que identifica la clase de error.
    public string Codigo { get; init; } = string.Empty;

    // Descripcion general del resultado fallido.
    public string Mensaje { get; init; } = string.Empty;

    // Mensajes de validacion agrupados por nombre de campo.
    public Dictionary<string, string[]> ErroresPorCampo { get; init; } = new();
}

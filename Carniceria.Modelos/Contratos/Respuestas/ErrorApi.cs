using System.Collections.Generic;

namespace Carniceria.Modelos.Contratos.Respuestas;

public sealed class ErrorApi
{
    public string Codigo { get; init; } = string.Empty;

    public string Mensaje { get; init; } = string.Empty;

    public Dictionary<string, string[]> ErroresPorCampo { get; init; } = new();
}

using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

public sealed class RespuestaProveedor
{
    public Guid Id { get; init; }

    public TipoIdentificacion TipoIdentificacion { get; init; }

    public string Identificacion { get; init; } = string.Empty;

    public string RazonSocial { get; init; } = string.Empty;

    public string NombreContacto { get; init; } = string.Empty;

    public string Telefono { get; init; } = string.Empty;

    public string CorreoElectronico { get; init; } = string.Empty;

    public TipoSuministro TipoSuministro { get; init; }

    public FrecuenciaEntrega FrecuenciaEntrega { get; init; }
}

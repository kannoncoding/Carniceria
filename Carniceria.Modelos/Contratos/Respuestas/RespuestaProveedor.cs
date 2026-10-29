using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Respuesta API con datos legales, comerciales y de contacto.
public sealed class RespuestaProveedor
{
    // Identificador del registro devuelto por la API.
    public Guid Id { get; init; }

    // Información legal y de contacto para mostrar el proveedor.
    public TipoIdentificacion TipoIdentificacion { get; init; }

    public string Identificacion { get; init; } = string.Empty;

    public string RazonSocial { get; init; } = string.Empty;

    public string NombreContacto { get; init; } = string.Empty;

    public string Telefono { get; init; } = string.Empty;

    public string CorreoElectronico { get; init; } = string.Empty;

    // Tipo de productos suministrados y frecuencia de entrega.
    public TipoSuministro TipoSuministro { get; init; }

    public FrecuenciaEntrega FrecuenciaEntrega { get; init; }
}

using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

public abstract class RespuestaPersona
{
    public Guid Id { get; init; }

    public TipoIdentificacion TipoIdentificacion { get; init; }

    public string Identificacion { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public string PrimerApellido { get; init; } = string.Empty;

    public string SegundoApellido { get; init; } = string.Empty;

    public DateTime FechaNacimiento { get; init; }
}

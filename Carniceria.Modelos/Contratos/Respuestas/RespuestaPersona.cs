using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Campos de respuesta compartidos por cliente y empleado.
public abstract class RespuestaPersona
{
    // Identificador del registro asignado por el almacén.
    public Guid Id { get; init; }

    // Datos de identificación y nombre que se exponen al cliente.
    public TipoIdentificacion TipoIdentificacion { get; init; }

    public string Identificacion { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public string PrimerApellido { get; init; } = string.Empty;

    public string SegundoApellido { get; init; } = string.Empty;

    // Fecha civil de nacimiento.
    public DateTime FechaNacimiento { get; init; }
}

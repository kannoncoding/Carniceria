using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

// Agrupa los datos personales comunes a clientes y empleados.
public abstract class Persona
{
    // Conserva la fecha de nacimiento sin componente de hora.
    private DateTime _fechaNacimiento;

    // Identificador interno asignado al crear la entidad.
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Clasificacion oficial de la identificacion.
    public TipoIdentificacion TipoIdentificacion { get; set; }

    // Numero de identificacion segun el tipo seleccionado.
    public string Identificacion { get; set; } = string.Empty;

    // Nombre y apellidos de la persona.
    public string Nombre { get; set; } = string.Empty;

    public string PrimerApellido { get; set; } = string.Empty;

    public string SegundoApellido { get; set; } = string.Empty;

    // La asignacion descarta la hora para mantener una fecha civil.
    public DateTime FechaNacimiento
    {
        get => _fechaNacimiento;
        set => _fechaNacimiento = value.Date;
    }
}

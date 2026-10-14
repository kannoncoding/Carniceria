using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

public abstract class Persona
{
    private DateTime _fechaNacimiento;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public TipoIdentificacion TipoIdentificacion { get; set; }

    public string Identificacion { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string PrimerApellido { get; set; } = string.Empty;

    public string SegundoApellido { get; set; } = string.Empty;

    public DateTime FechaNacimiento
    {
        get => _fechaNacimiento;
        set => _fechaNacimiento = value.Date;
    }
}
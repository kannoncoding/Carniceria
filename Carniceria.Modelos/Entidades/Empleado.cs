using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

public class Empleado : Persona
{
    private DateTime _fechaIngreso;

    public decimal SalarioMensual { get; set; }

    public DateTime FechaIngreso
    {
        get => _fechaIngreso;
        set => _fechaIngreso = value.Date;
    }

    public AreaTrabajo AreaTrabajo { get; set; }

    public string Puesto { get; set; } = string.Empty;

    public Turno Turno { get; set; }

    public Empleado Copiar()
    {
        return (Empleado)MemberwiseClone();
    }
}
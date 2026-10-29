using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

// Representa una persona con información laboral asignada.
public class Empleado : Persona
{
    // Conserva la fecha de ingreso sin componente de hora.
    private DateTime _fechaIngreso;

    // Remuneración mensual registrada para el empleado.
    public decimal SalarioMensual { get; set; }

    // Normaliza la fecha de ingreso al asignarla.
    public DateTime FechaIngreso
    {
        get => _fechaIngreso;
        set => _fechaIngreso = value.Date;
    }

    // Área, puesto y turno asignados al empleado.
    public AreaTrabajo AreaTrabajo { get; set; }

    public string Puesto { get; set; } = string.Empty;

    public Turno Turno { get; set; }

    // Devuelve una copia independiente de los datos simples de la entidad.
    public Empleado Copiar()
    {
        return (Empleado)MemberwiseClone();
    }
}

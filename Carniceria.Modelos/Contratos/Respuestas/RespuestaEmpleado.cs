using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Respuesta API con datos personales y laborales del empleado.
public sealed class RespuestaEmpleado : RespuestaPersona
{
    // Información laboral adicional a los datos personales heredados.
    public decimal SalarioMensual { get; init; }

    public DateTime FechaIngreso { get; init; }

    public AreaTrabajo AreaTrabajo { get; init; }

    public string Puesto { get; init; } = string.Empty;

    public Turno Turno { get; init; }
}

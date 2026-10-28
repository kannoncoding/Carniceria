using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

public sealed class RespuestaEmpleado : RespuestaPersona
{
    public decimal SalarioMensual { get; init; }

    public DateTime FechaIngreso { get; init; }

    public AreaTrabajo AreaTrabajo { get; init; }

    public string Puesto { get; init; } = string.Empty;

    public Turno Turno { get; init; }
}

using System;
using System.Text.Json.Serialization;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Respuesta API con datos personales y laborales del empleado.
public sealed class RespuestaEmpleado : RespuestaPersona
{
    // Informacion laboral adicional a los datos personales heredados.
    public decimal SalarioMensual { get; init; }

    // Fecha de ingreso requerida en las respuestas de empleados.
    [JsonRequired]
    public DateTime FechaIngreso { get; init; }

    public AreaTrabajo AreaTrabajo { get; init; }

    public string Puesto { get; init; } = string.Empty;

    public Turno Turno { get; init; }
}

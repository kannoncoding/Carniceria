using System;
using System.Collections.Generic;
using System.Linq;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Catalogos;

public static class CatalogoPuestos
{
    private static readonly Dictionary<AreaTrabajo, IReadOnlyList<string>>
        PuestosPorArea = new()
        {
            [AreaTrabajo.Administracion] = Array.AsReadOnly(new[]
            {
                "Administrador",
                "Asistente administrativo"
            }),

            [AreaTrabajo.VentasYCaja] = Array.AsReadOnly(new[]
            {
                "Cajero",
                "Vendedor",
                "Servicio al cliente"
            }),

            [AreaTrabajo.CorteYPreparacion] = Array.AsReadOnly(new[]
            {
                "Carnicero",
                "Despostador",
                "Empacador"
            }),

            [AreaTrabajo.BodegaEInventario] = Array.AsReadOnly(new[]
            {
                "Bodeguero",
                "Auxiliar de inventario"
            }),

            [AreaTrabajo.Distribucion] = Array.AsReadOnly(new[]
            {
                "Conductor",
                "Ayudante de distribución"
            }),

            [AreaTrabajo.Limpieza] = Array.AsReadOnly(new[]
            {
                "Personal de limpieza"
            })
        };

    public static IReadOnlyList<string> ObtenerPuestos(AreaTrabajo area)
    {
        if (PuestosPorArea.TryGetValue(area, out var puestos))
        {
            return puestos;
        }

        return Array.Empty<string>();
    }

    public static bool EsPuestoValido(AreaTrabajo area, string? puesto)
    {
        if (string.IsNullOrWhiteSpace(puesto))
        {
            return false;
        }

        string puestoNormalizado = puesto.Trim();

        return ObtenerPuestos(area).Contains(
            puestoNormalizado,
            StringComparer.OrdinalIgnoreCase);
    }
}
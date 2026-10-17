using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Carniceria.Modelos.Catalogos;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Carniceria.Web.Helpers;

public static class OpcionesFormulario
{
    public static IReadOnlyList<SelectListItem> TiposIdentificacionCliente(
        this IHtmlHelper html)
    {
        return FiltrarTiposIdentificacion(
            html,
            TipoIdentificacion.CedulaNacional,
            TipoIdentificacion.Dimex,
            TipoIdentificacion.Pasaporte);
    }

    public static IReadOnlyList<SelectListItem> TiposIdentificacionEmpleado(
        this IHtmlHelper html)
    {
        return FiltrarTiposIdentificacion(
            html,
            TipoIdentificacion.CedulaNacional,
            TipoIdentificacion.Dimex);
    }

    public static IReadOnlyList<SelectListItem> TiposIdentificacionProveedor(
        this IHtmlHelper html)
    {
        return FiltrarTiposIdentificacion(
            html,
            TipoIdentificacion.CedulaNacional,
            TipoIdentificacion.CedulaJuridica,
            TipoIdentificacion.Dimex);
    }

    public static IReadOnlyList<SelectListItem> PuestosEmpleado(
        AreaTrabajo? areaTrabajo)
    {
        if (!areaTrabajo.HasValue)
        {
            return Array.Empty<SelectListItem>();
        }

        return CatalogoPuestos
            .ObtenerPuestos(areaTrabajo.Value)
            .Select(puesto => new SelectListItem
            {
                Value = puesto,
                Text = puesto
            })
            .ToArray();
    }

    private static IReadOnlyList<SelectListItem> FiltrarTiposIdentificacion(
        IHtmlHelper html,
        params TipoIdentificacion[] tiposPermitidos)
    {
        ArgumentNullException.ThrowIfNull(html);

        var valoresPermitidos = tiposPermitidos
            .Select(tipo => ((int)tipo).ToString(
                CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);

        return html
            .GetEnumSelectList<TipoIdentificacion>()
            .Where(opcion =>
                opcion.Value is not null &&
                valoresPermitidos.Contains(opcion.Value))
            .ToArray();
    }
}
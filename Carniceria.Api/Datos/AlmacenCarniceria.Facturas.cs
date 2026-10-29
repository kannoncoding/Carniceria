using System;
using System.Globalization;
using System.Linq;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    // Asigna identidad a un candidato validado bajo el bloqueo del almacén.
    private Factura PrepararFacturaValidadaParaGuardar(Factura candidata)
    {
        ArgumentNullException.ThrowIfNull(candidata);
        ArgumentNullException.ThrowIfNull(candidata.Detalles);

        return EjecutarConBloqueo(() =>
        {
            var factura = candidata.Copiar();
            factura.Numero = "FAC" + _siguienteNumeroFactura.ToString(
                "D6",
                CultureInfo.InvariantCulture);
            factura.FechaEmision = TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica"));
            factura.Estado = EstadoFactura.Pendiente;
            factura.Detalles = candidata.Detalles
                .Select(detalle => new DetalleFactura
                {
                    ProductoId = detalle.ProductoId,
                    UnidadMedida = detalle.UnidadMedida,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = detalle.PrecioUnitario,
                    SubtotalLinea = detalle.SubtotalLinea
                })
                .ToList();

            _siguienteNumeroFactura++;
            return factura;
        });
    }
}

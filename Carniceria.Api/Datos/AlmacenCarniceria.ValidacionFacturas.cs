using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Carniceria.Api.Reglas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    // Valida relaciones y lineas sin reservar identidad ni modificar el almacen.
    private IReadOnlyList<ValidationResult> ValidarSolicitudFactura(
        SolicitudFactura? solicitud)
    {
        var errores = new List<ValidationResult>();

        if (solicitud is null)
        {
            errores.Add(new ValidationResult(
                "La solicitud de factura es obligatoria.",
                new[] { "Solicitud" }));
            return errores.AsReadOnly();
        }

        if (solicitud.ClienteId == Guid.Empty)
        {
            errores.Add(new ValidationResult(
                "Seleccione un cliente válido.",
                new[] { nameof(solicitud.ClienteId) }));
        }

        if (solicitud.EmpleadoId == Guid.Empty)
        {
            errores.Add(new ValidationResult(
                "Seleccione un empleado válido.",
                new[] { nameof(solicitud.EmpleadoId) }));
        }

        if (!Enum.IsDefined(typeof(MetodoPago), solicitud.MetodoPago))
        {
            errores.Add(new ValidationResult(
                "Seleccione un método de pago permitido.",
                new[] { nameof(solicitud.MetodoPago) }));
        }

        if (solicitud.PorcentajeDescuento < 0 ||
            solicitud.PorcentajeDescuento > ReglasFacturacion.PorcentajeDescuentoMaximo)
        {
            errores.Add(new ValidationResult(
                $"El descuento debe estar entre 0 y {ReglasFacturacion.PorcentajeDescuentoMaximo}.",
                new[] { nameof(solicitud.PorcentajeDescuento) }));
        }

        if (solicitud.Detalles is null)
        {
            errores.Add(new ValidationResult(
                "Incluya al menos una línea de detalle.",
                new[] { nameof(solicitud.Detalles) }));
        }
        else if (solicitud.Detalles.Count == 0)
        {
            errores.Add(new ValidationResult(
                "Incluya al menos una línea de detalle.",
                new[] { nameof(solicitud.Detalles) }));
        }

        // Comprueba relaciones, productos y cantidades en una sola lectura.
        EjecutarConBloqueo(() =>
        {
            if (solicitud.ClienteId != Guid.Empty &&
                !_clientes.Any(cliente => cliente.Id == solicitud.ClienteId))
            {
                errores.Add(new ValidationResult(
                    "Seleccione un cliente existente.",
                    new[] { nameof(solicitud.ClienteId) }));
            }

            if (solicitud.EmpleadoId != Guid.Empty &&
                !_empleados.Any(empleado => empleado.Id == solicitud.EmpleadoId))
            {
                errores.Add(new ValidationResult(
                    "Seleccione un empleado existente.",
                    new[] { nameof(solicitud.EmpleadoId) }));
            }

            if (solicitud.Detalles is not null)
            {
                for (var indice = 0; indice < solicitud.Detalles.Count; indice++)
                {
                    var detalle = solicitud.Detalles[indice];
                    var prefijoCampo = $"Detalles[{indice}]";

                    if (detalle is null)
                    {
                        errores.Add(new ValidationResult(
                            "La línea de detalle es obligatoria.",
                            new[] { prefijoCampo }));
                        continue;
                    }

                    if (detalle.ProductoId == Guid.Empty ||
                        !_productos.Any(producto => producto.Id == detalle.ProductoId))
                    {
                        errores.Add(new ValidationResult(
                            "Seleccione un producto existente.",
                            new[] { $"{prefijoCampo}.{nameof(detalle.ProductoId)}" }));
                    }

                    if (detalle.Cantidad <= 0)
                    {
                        errores.Add(new ValidationResult(
                            "La cantidad debe ser mayor que cero.",
                            new[] { $"{prefijoCampo}.{nameof(detalle.Cantidad)}" }));
                    }
                }
            }

            return true;
        });

        return errores.AsReadOnly();
    }
}

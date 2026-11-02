using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Carniceria.Api.Reglas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    // Actualiza una factura pendiente solo despues de validar el candidato completo.
    public ResultadoOperacion ActualizarFacturaPendiente(
        string numero,
        SolicitudEdicionFactura solicitud)
    {
        var numeroNormalizado = numero?.Trim() ?? string.Empty;
        if (numeroNormalizado.Length == 0)
        {
            return ResultadoOperacion.NoEncontrado(
                "La factura solicitada no existe.");
        }

        lock (_sincronizacion)
        {
            var indiceFactura = _facturas.FindIndex(factura =>
                string.Equals(
                    factura.Numero,
                    numeroNormalizado,
                    StringComparison.OrdinalIgnoreCase));
            if (indiceFactura < 0)
            {
                return ResultadoOperacion.NoEncontrado(
                    "La factura solicitada no existe.");
            }

            var facturaActual = _facturas[indiceFactura];
            var conflictoTransicion = ReglasTransicionFactura.ValidarTransicion(
                facturaActual.Estado,
                OperacionFactura.Editar);
            if (conflictoTransicion is not null)
            {
                return conflictoTransicion;
            }

            if (facturaActual.Estado != EstadoFactura.Pendiente)
            {
                return ResultadoOperacion.ConflictoIntegridad(
                    nameof(Factura.Estado),
                    "La factura no está Pendiente.");
            }

            var solicitudBase = CrearSolicitudBaseEdicion(solicitud);
            var errores = ValidarSolicitudFactura(solicitudBase).ToList();
            errores.AddRange(ValidarIdsDetallesEdicion(facturaActual, solicitud));
            if (errores.Count > 0)
            {
                return ResultadoOperacion.ValidacionFallida(errores);
            }

            // Prepara cada linea con su Id y snapshot segun la opcion elegida.
            var detallesPreparados = new List<DetalleFactura>();
            // Agrupa la demanda para comprobar stock sin reservar existencias.
            var demandaPorProducto = new Dictionary<Guid, decimal>();
            for (var indice = 0; indice < solicitud.Detalles!.Count; indice++)
            {
                var solicitudDetalle = solicitud.Detalles[indice]!;
                var producto = _productos.First(registro =>
                    registro.Id == solicitudDetalle.ProductoId);
                var detalleExistente = solicitudDetalle.Id.HasValue
                    ? facturaActual.Detalles.First(detalle =>
                        detalle.Id == solicitudDetalle.Id.Value)
                    : null;
                var conservarSnapshot = detalleExistente is not null &&
                    detalleExistente.ProductoId == producto.Id &&
                    !solicitud.ActualizarPreciosYUnidades;
                var detallePreparado = detalleExistente?.Copiar() ?? new DetalleFactura();

                detallePreparado.ProductoId = producto.Id;
                detallePreparado.Cantidad = solicitudDetalle.Cantidad;
                if (!conservarSnapshot)
                {
                    detallePreparado.UnidadMedida = producto.UnidadMedida;
                    detallePreparado.PrecioUnitario = producto.PrecioUnidadMedida;
                }

                if (detallePreparado.UnidadMedida != producto.UnidadMedida)
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indice}].{nameof(DetalleFactura.UnidadMedida)}",
                        "La unidad capturada no coincide con la unidad actual del producto.");
                }

                try
                {
                    demandaPorProducto[producto.Id] =
                        demandaPorProducto.GetValueOrDefault(producto.Id) +
                        detallePreparado.Cantidad;
                }
                catch (OverflowException)
                {
                    return ResultadoOperacion.ValidacionFallida(new[]
                    {
                        new ValidationResult(
                            "La demanda de las lineas excede el rango permitido.",
                            new[] { $"Detalles[{indice}].{nameof(solicitudDetalle.Cantidad)}" })
                    });
                }

                detallesPreparados.Add(detallePreparado);
            }

            foreach (var demanda in demandaPorProducto)
            {
                var producto = _productos.First(registro =>
                    registro.Id == demanda.Key);
                if (demanda.Value > producto.ExistenciaActual)
                {
                    var indice = solicitud.Detalles.FindIndex(detalle =>
                        detalle is not null && detalle.ProductoId == producto.Id);
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indice}].{nameof(SolicitudDetalleEdicionFactura.Cantidad)}",
                        $"La existencia de {producto.Nombre} no cubre la cantidad total solicitada.");
                }
            }

            // Actualiza solo los campos editables en una copia profunda.
            var candidata = facturaActual.Copiar();
            candidata.ClienteId = solicitud.ClienteId;
            candidata.EmpleadoId = solicitud.EmpleadoId;
            candidata.MetodoPago = solicitud.MetodoPago;
            candidata.PorcentajeDescuento = solicitud.PorcentajeDescuento;
            candidata.Detalles = detallesPreparados;

            var calculo = CalcularImportesFactura(candidata);
            if (calculo.Error is not null)
            {
                return calculo.Error;
            }

            _facturas[indiceFactura] = calculo.Factura!;
            return ResultadoOperacion.Correcto(
                facturaActual.Id,
                "La factura pendiente se actualizó correctamente.");
        }
    }

    // Convierte los campos de entrada al contrato ya validado para una factura nueva.
    private static SolicitudFactura? CrearSolicitudBaseEdicion(
        SolicitudEdicionFactura? solicitud)
    {
        if (solicitud is null)
        {
            return null;
        }

        return new SolicitudFactura
        {
            ClienteId = solicitud.ClienteId,
            EmpleadoId = solicitud.EmpleadoId,
            MetodoPago = solicitud.MetodoPago,
            PorcentajeDescuento = solicitud.PorcentajeDescuento,
            Detalles = solicitud.Detalles?.Select(detalle => detalle is null
                ? null!
                : new SolicitudDetalleFactura
                {
                    ProductoId = detalle.ProductoId,
                    Cantidad = detalle.Cantidad
                }).ToList()!
        };
    }

    // Valida que cada Id opcional sea propio, no vacio y no repetido.
    private static IReadOnlyList<ValidationResult> ValidarIdsDetallesEdicion(
        Factura factura,
        SolicitudEdicionFactura? solicitud)
    {
        var errores = new List<ValidationResult>();
        if (solicitud?.Detalles is null)
        {
            return errores.AsReadOnly();
        }

        var idsUsados = new HashSet<Guid>();
        for (var indice = 0; indice < solicitud.Detalles.Count; indice++)
        {
            var detalle = solicitud.Detalles[indice];
            if (detalle?.Id is not Guid id)
            {
                continue;
            }

            var campo = $"Detalles[{indice}].{nameof(detalle.Id)}";
            if (id == Guid.Empty)
            {
                errores.Add(new ValidationResult(
                    "El Id de una linea existente no puede estar vacio.",
                    new[] { campo }));
                continue;
            }

            if (!factura.Detalles.Any(linea => linea.Id == id))
            {
                errores.Add(new ValidationResult(
                    "La linea indicada no pertenece a esta factura.",
                    new[] { campo }));
                continue;
            }

            if (!idsUsados.Add(id))
            {
                errores.Add(new ValidationResult(
                    "El Id de linea no puede repetirse en la solicitud.",
                    new[] { campo }));
            }
        }

        return errores.AsReadOnly();
    }
}

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
    // Actualiza la factura permitida despues de validar todos sus cambios.
    public ResultadoOperacion ActualizarFactura(
        string numero,
        SolicitudEdicionFactura solicitud)
    {
        lock (_sincronizacion)
        {
            var preparacion = PrepararEdicionFactura(numero, solicitud);
            if (preparacion.Error is not null)
            {
                return preparacion.Error;
            }

            var facturaPreparada = preparacion.Factura!;
            foreach (var (producto, existencia) in preparacion.ExistenciasPreparadas)
            {
                producto.ExistenciaActual = existencia;
            }

            _facturas[preparacion.IndiceFactura] = facturaPreparada;
            return ResultadoOperacion.Correcto(
                facturaPreparada.Id,
                facturaPreparada.Estado == EstadoFactura.Pagada
                    ? "La factura pagada se actualizó correctamente."
                    : "La factura pendiente se actualizó correctamente.");
        }
    }

    // Calcula una edicion sin aplicar existencias ni reemplazar la factura.
    public (ResultadoOperacion? Error, Factura? Factura) PrevisualizarEdicionFactura(
        string numero,
        SolicitudEdicionFactura solicitud)
    {
        lock (_sincronizacion)
        {
            var preparacion = PrepararEdicionFactura(numero, solicitud);
            return (preparacion.Error, preparacion.Factura?.Copiar());
        }
    }

    // Reutiliza validacion y calculo para la vista previa y el guardado.
    private (ResultadoOperacion? Error, int IndiceFactura, Factura? Factura,
        List<(Producto Producto, decimal Existencia)> ExistenciasPreparadas)
        PrepararEdicionFactura(string numero, SolicitudEdicionFactura solicitud)
    {
        var numeroNormalizado = numero?.Trim() ?? string.Empty;
        if (numeroNormalizado.Length == 0)
        {
            return (ResultadoOperacion.NoEncontrado(
                "La factura solicitada no existe."), -1, null, new());
        }

        var indiceFactura = _facturas.FindIndex(factura =>
            string.Equals(
                factura.Numero,
                numeroNormalizado,
                StringComparison.OrdinalIgnoreCase));
        if (indiceFactura < 0)
        {
            return (ResultadoOperacion.NoEncontrado(
                "La factura solicitada no existe."), -1, null, new());
        }

        var facturaActual = _facturas[indiceFactura];
        var conflictoTransicion = ReglasTransicionFactura.ValidarTransicion(
            facturaActual.Estado,
            OperacionFactura.Editar);
        if (conflictoTransicion is not null)
        {
            return (conflictoTransicion, -1, null, new());
        }

        var esPagada = facturaActual.Estado == EstadoFactura.Pagada;
        var solicitudBase = CrearSolicitudBaseEdicion(solicitud);
        var errores = ValidarSolicitudFactura(solicitudBase).ToList();
        errores.AddRange(ValidarIdsDetallesEdicion(facturaActual, solicitud));
        if (errores.Count > 0)
        {
            return (ResultadoOperacion.ValidacionFallida(errores), -1, null, new());
        }

        // Prepara cada linea con su Id y valores capturados segun la opcion elegida.
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
            var conservarValoresHistoricos = detalleExistente is not null &&
                detalleExistente.ProductoId == producto.Id &&
                !solicitud.ActualizarPreciosYUnidades;
            var detallePreparado = detalleExistente?.Copiar() ?? new DetalleFactura();

            detallePreparado.ProductoId = producto.Id;
            detallePreparado.Cantidad = solicitudDetalle.Cantidad;
            if (!conservarValoresHistoricos)
            {
                detallePreparado.UnidadMedida = producto.UnidadMedida;
                detallePreparado.PrecioUnitario = producto.PrecioUnidadMedida;
            }

            if (detallePreparado.UnidadMedida != producto.UnidadMedida)
            {
                return (ResultadoOperacion.ConflictoIntegridad(
                    $"Detalles[{indice}].{nameof(DetalleFactura.UnidadMedida)}",
                    "La unidad capturada no coincide con la unidad actual del producto."),
                    -1, null, new());
            }

            try
            {
                demandaPorProducto[producto.Id] =
                    demandaPorProducto.GetValueOrDefault(producto.Id) +
                    detallePreparado.Cantidad;
            }
            catch (OverflowException)
            {
                return (ResultadoOperacion.ValidacionFallida(new[]
                {
                    new ValidationResult(
                        "La demanda de las lineas excede el rango permitido.",
                        new[] { $"Detalles[{indice}].{nameof(solicitudDetalle.Cantidad)}" })
                }), -1, null, new());
            }

            detallesPreparados.Add(detallePreparado);
        }

        // Agrupa las cantidades que ya fueron descontadas al pagar.
        var demandaAnteriorPorProducto = new Dictionary<Guid, decimal>();
        if (esPagada)
        {
            for (var indice = 0; indice < facturaActual.Detalles.Count; indice++)
            {
                var detalle = facturaActual.Detalles[indice];
                var producto = _productos.FirstOrDefault(registro =>
                    registro.Id == detalle.ProductoId);
                if (producto is null || detalle.UnidadMedida != producto.UnidadMedida)
                {
                    return (ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indice}].{nameof(DetalleFactura.UnidadMedida)}",
                        "La unidad actual del producto no coincide con la unidad capturada en la factura."),
                        -1, null, new());
                }

                try
                {
                    demandaAnteriorPorProducto[detalle.ProductoId] =
                        demandaAnteriorPorProducto.GetValueOrDefault(detalle.ProductoId) +
                        detalle.Cantidad;
                }
                catch (OverflowException)
                {
                    return (ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indice}].{nameof(DetalleFactura.Cantidad)}",
                        "La demanda anterior de la factura excede el rango permitido."),
                        -1, null, new());
                }
            }
        }

        // Prepara existencias candidatas sin modificar los productos.
        var existenciasPreparadas = new List<(Producto Producto, decimal Existencia)>();
        var productosAValidar = esPagada
            ? demandaAnteriorPorProducto.Keys
                .Union(demandaPorProducto.Keys)
                .ToArray()
            : demandaPorProducto.Keys.ToArray();
        foreach (var idProducto in productosAValidar)
        {
            var producto = _productos.FirstOrDefault(registro =>
                registro.Id == idProducto);
            if (producto is null)
            {
                return (ResultadoOperacion.ConflictoIntegridad(
                    nameof(DetalleFactura.ProductoId),
                    "Un producto de la factura ya no está disponible."), -1, null, new());
            }

            var demandaNueva = demandaPorProducto.GetValueOrDefault(idProducto);
            if (esPagada)
            {
                var demandaAnterior = demandaAnteriorPorProducto.GetValueOrDefault(idProducto);
                if (producto.ExistenciaActual < 0m)
                {
                    return (ResultadoOperacion.ConflictoIntegridad(
                        nameof(Producto.ExistenciaActual),
                        "El producto tiene existencias inválidas."), -1, null, new());
                }

                try
                {
                    var diferenciaDemanda = demandaAnterior - demandaNueva;
                    var existenciaFinal = producto.ExistenciaActual + diferenciaDemanda;
                    if (existenciaFinal < 0m)
                    {
                        return (ResultadoOperacion.ConflictoIntegridad(
                            nameof(Producto.ExistenciaActual),
                            $"La existencia de {producto.Nombre} no cubre la factura editada."),
                            -1, null, new());
                    }

                    existenciasPreparadas.Add((producto, existenciaFinal));
                }
                catch (OverflowException)
                {
                    return (ResultadoOperacion.ConflictoIntegridad(
                        nameof(Producto.ExistenciaActual),
                        "La existencia final del producto excede el rango permitido."),
                        -1, null, new());
                }

                continue;
            }

            if (demandaNueva > producto.ExistenciaActual)
            {
                var indice = solicitud.Detalles.FindIndex(detalle =>
                    detalle is not null && detalle.ProductoId == producto.Id);
                return (ResultadoOperacion.ConflictoIntegridad(
                    $"Detalles[{indice}].{nameof(SolicitudDetalleEdicionFactura.Cantidad)}",
                    $"La existencia de {producto.Nombre} no cubre la cantidad total solicitada."),
                    -1, null, new());
            }
        }

        // Calcula la factura candidata completa antes de cualquier escritura.
        var candidata = facturaActual.Copiar();
        candidata.ClienteId = solicitud.ClienteId;
        candidata.EmpleadoId = solicitud.EmpleadoId;
        candidata.MetodoPago = solicitud.MetodoPago;
        candidata.PorcentajeDescuento = solicitud.PorcentajeDescuento;
        candidata.Detalles = detallesPreparados;

        var calculo = CalcularImportesFactura(candidata);
        if (calculo.Error is not null)
        {
            return (calculo.Error, -1, null, new());
        }

        return (null, indiceFactura, calculo.Factura, existenciasPreparadas);
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

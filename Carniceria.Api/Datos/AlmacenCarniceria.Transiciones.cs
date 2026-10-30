using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Carniceria.Api.Reglas;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    // Paga una factura pendiente con cambios preparados antes de actualizar datos.
    public ResultadoOperacion PagarFactura(string numero)
    {
        var numeroNormalizado = numero?.Trim() ?? string.Empty;
        if (numeroNormalizado.Length == 0)
        {
            return ResultadoOperacion.NoEncontrado(
                "La factura solicitada no existe.");
        }

        lock (_sincronizacion)
        {
            var factura = _facturas.FirstOrDefault(registro =>
                string.Equals(
                    registro.Numero,
                    numeroNormalizado,
                    StringComparison.OrdinalIgnoreCase));
            if (factura is null)
            {
                return ResultadoOperacion.NoEncontrado(
                    "La factura solicitada no existe.");
            }

            var conflictoTransicion = ReglasTransicionFactura.ValidarTransicion(
                factura.Estado,
                OperacionFactura.Pagar);
            if (conflictoTransicion is not null)
            {
                return conflictoTransicion;
            }

            // Agrupa cantidades de todas las lineas del mismo producto.
            var demandaPorProducto = new Dictionary<Guid, decimal>();
            for (var indice = 0; indice < factura.Detalles.Count; indice++)
            {
                var detalle = factura.Detalles[indice];
                try
                {
                    demandaPorProducto[detalle.ProductoId] =
                        demandaPorProducto.GetValueOrDefault(detalle.ProductoId) +
                        detalle.Cantidad;
                }
                catch (OverflowException)
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indice}].{nameof(DetalleFactura.Cantidad)}",
                        "La demanda de la factura excede el rango permitido.");
                }
            }

            // Guarda los stocks finales sin aplicarlos antes de validar todo.
            var existenciasPreparadas = new List<(Producto Producto, decimal Existencia)>();
            foreach (var demanda in demandaPorProducto)
            {
                var producto = _productos.FirstOrDefault(registro =>
                    registro.Id == demanda.Key);
                if (producto is null)
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        nameof(DetalleFactura.ProductoId),
                        "Un producto de la factura ya no está disponible.");
                }

                var indiceDetalle = factura.Detalles.FindIndex(detalle =>
                    detalle.ProductoId == producto.Id);
                if (factura.Detalles.Any(detalle =>
                        detalle.ProductoId == producto.Id &&
                        detalle.UnidadMedida != producto.UnidadMedida))
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indiceDetalle}].{nameof(DetalleFactura.UnidadMedida)}",
                        "La unidad actual del producto no coincide con la unidad capturada en la factura.");
                }

                if (producto.ExistenciaActual < 0m || demanda.Value < 0m)
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indiceDetalle}].{nameof(DetalleFactura.Cantidad)}",
                        "La factura o el producto tiene existencias inválidas.");
                }

                if (demanda.Value > producto.ExistenciaActual)
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indiceDetalle}].{nameof(DetalleFactura.Cantidad)}",
                        $"La existencia actual de {producto.Nombre} no cubre la factura.");
                }

                try
                {
                    var existenciaFinal = producto.ExistenciaActual - demanda.Value;
                    existenciasPreparadas.Add((producto, existenciaFinal));
                }
                catch (OverflowException)
                {
                    return ResultadoOperacion.ConflictoIntegridad(
                        $"Detalles[{indiceDetalle}].{nameof(DetalleFactura.Cantidad)}",
                        "La existencia final del producto excede el rango permitido.");
                }
            }

            // Prepara el estado final antes de modificar factura o productos.
            var facturaPagada = factura.Copiar();
            facturaPagada.Estado = EstadoFactura.Pagada;

            foreach (var (producto, existencia) in existenciasPreparadas)
            {
                producto.ExistenciaActual = existencia;
            }

            factura.Estado = facturaPagada.Estado;
            return ResultadoOperacion.Correcto(
                factura.Id,
                "La factura se pagó correctamente.");
        }
    }
}

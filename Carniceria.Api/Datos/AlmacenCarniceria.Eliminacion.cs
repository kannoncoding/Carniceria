using System;
using System.Linq;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    public ResultadoOperacion EliminarCliente(Guid id)
    {
        lock (_sincronizacion)
        {
            var cliente = _clientes.FirstOrDefault(
                registro => registro.Id == id);

            if (cliente is null)
            {
                return ResultadoOperacion.NoEncontrado(
                    "El cliente solicitado no existe.");
            }

            _clientes.Remove(cliente);

            return ResultadoOperacion.Correcto(
                cliente.Id,
                "Cliente eliminado correctamente.");
        }
    }

    public ResultadoOperacion EliminarEmpleado(Guid id)
    {
        lock (_sincronizacion)
        {
            var empleado = _empleados.FirstOrDefault(
                registro => registro.Id == id);

            if (empleado is null)
            {
                return ResultadoOperacion.NoEncontrado(
                    "El empleado solicitado no existe.");
            }

            _empleados.Remove(empleado);

            return ResultadoOperacion.Correcto(
                empleado.Id,
                "Empleado eliminado correctamente.");
        }
    }

    public ResultadoOperacion EliminarProducto(Guid id)
    {
        lock (_sincronizacion)
        {
            var producto = _productos.FirstOrDefault(
                registro => registro.Id == id);

            if (producto is null)
            {
                return ResultadoOperacion.NoEncontrado(
                    "El producto solicitado no existe.");
            }

            _productos.Remove(producto);

            return ResultadoOperacion.Correcto(
                producto.Id,
                "Producto eliminado correctamente.");
        }
    }

    public ResultadoOperacion EliminarProveedor(Guid id)
    {
        lock (_sincronizacion)
        {
            var proveedor = _proveedores.FirstOrDefault(
                registro => registro.Id == id);

            if (proveedor is null)
            {
                return ResultadoOperacion.NoEncontrado(
                    "El proveedor solicitado no existe.");
            }

            _proveedores.Remove(proveedor);

            return ResultadoOperacion.Correcto(
                proveedor.Id,
                "Proveedor eliminado correctamente.");
        }
    }
}
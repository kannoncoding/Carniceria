using System;
using System.Collections.Generic;
using System.Linq;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Validacion;

namespace Carniceria.Web.Data;

public partial class AlmacenCarniceria
{
    private readonly object _sincronizacion = new();

    private readonly List<Cliente> _clientes = new();

    private readonly List<Empleado> _empleados = new();

    private readonly List<Producto> _productos = new();

    private readonly List<Proveedor> _proveedores = new();

    // Clientes

    public IReadOnlyList<Cliente> ListarClientes()
    {
        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _clientes
                    .Select(cliente => cliente.Copiar())
                    .ToArray());
        }
    }

    public Cliente? ObtenerCliente(Guid id)
    {
        lock (_sincronizacion)
        {
            return _clientes
                .FirstOrDefault(cliente => cliente.Id == id)
                ?.Copiar();
        }
    }

    public IReadOnlyList<Cliente> BuscarClientesPorIdentificacion(
        string identificacion)
    {
        string consulta = ReglasTexto.Normalizar(identificacion);

        if (consulta.Length == 0)
        {
            return Array.Empty<Cliente>();
        }

        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _clientes
                    .Where(cliente => string.Equals(
                        cliente.Identificacion,
                        consulta,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(cliente => cliente.Copiar())
                    .ToArray());
        }
    }

    // Empleados

    public IReadOnlyList<Empleado> ListarEmpleados()
    {
        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _empleados
                    .Select(empleado => empleado.Copiar())
                    .ToArray());
        }
    }

    public Empleado? ObtenerEmpleado(Guid id)
    {
        lock (_sincronizacion)
        {
            return _empleados
                .FirstOrDefault(empleado => empleado.Id == id)
                ?.Copiar();
        }
    }

    public IReadOnlyList<Empleado> BuscarEmpleadosPorIdentificacion(
        string identificacion)
    {
        string consulta = ReglasTexto.Normalizar(identificacion);

        if (consulta.Length == 0)
        {
            return Array.Empty<Empleado>();
        }

        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _empleados
                    .Where(empleado => string.Equals(
                        empleado.Identificacion,
                        consulta,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(empleado => empleado.Copiar())
                    .ToArray());
        }
    }

    // Productos

    public IReadOnlyList<Producto> ListarProductos()
    {
        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _productos
                    .Select(producto => producto.Copiar())
                    .ToArray());
        }
    }

    public Producto? ObtenerProducto(Guid id)
    {
        lock (_sincronizacion)
        {
            return _productos
                .FirstOrDefault(producto => producto.Id == id)
                ?.Copiar();
        }
    }

    public IReadOnlyList<Producto> BuscarProductosPorCodigo(
        string codigo)
    {
        string consulta = ReglasTexto.Normalizar(codigo);

        if (consulta.Length == 0)
        {
            return Array.Empty<Producto>();
        }

        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _productos
                    .Where(producto => string.Equals(
                        producto.Codigo,
                        consulta,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(producto => producto.Copiar())
                    .ToArray());
        }
    }

    public IReadOnlyList<Producto> BuscarProductosPorNombre(
        string nombre)
    {
        string consulta = ReglasTexto.Normalizar(nombre);

        if (consulta.Length == 0)
        {
            return Array.Empty<Producto>();
        }

        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _productos
                    .Where(producto => producto.Nombre.Contains(
                        consulta,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(producto => producto.Copiar())
                    .ToArray());
        }
    }

    // Proveedores

    public IReadOnlyList<Proveedor> ListarProveedores()
    {
        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _proveedores
                    .Select(proveedor => proveedor.Copiar())
                    .ToArray());
        }
    }

    public Proveedor? ObtenerProveedor(Guid id)
    {
        lock (_sincronizacion)
        {
            return _proveedores
                .FirstOrDefault(proveedor => proveedor.Id == id)
                ?.Copiar();
        }
    }

    public IReadOnlyList<Proveedor> BuscarProveedoresPorIdentificacion(
        string identificacion)
    {
        string consulta = ReglasTexto.Normalizar(identificacion);

        if (consulta.Length == 0)
        {
            return Array.Empty<Proveedor>();
        }

        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _proveedores
                    .Where(proveedor => string.Equals(
                        proveedor.Identificacion,
                        consulta,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(proveedor => proveedor.Copiar())
                    .ToArray());
        }
    }

    public IReadOnlyList<Proveedor> BuscarProveedoresPorRazonSocial(
        string razonSocial)
    {
        string consulta = ReglasTexto.Normalizar(razonSocial);

        if (consulta.Length == 0)
        {
            return Array.Empty<Proveedor>();
        }

        lock (_sincronizacion)
        {
            return Array.AsReadOnly(
                _proveedores
                    .Where(proveedor => proveedor.RazonSocial.Contains(
                        consulta,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(proveedor => proveedor.Copiar())
                    .ToArray());
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Validacion;

namespace Carniceria.Api.Datos;

// Mantiene los registros en memoria y coordina su acceso concurrente.
public partial class AlmacenCarniceria : IAlmacenCarniceria
{
    // Protege las colecciones y operaciones compuestas del almacen.
    private readonly object _sincronizacion = new();

    // Registros en memoria separados por tipo de entidad.
    private readonly List<Cliente> _clientes = new();

    private readonly List<Empleado> _empleados = new();

    private readonly List<Producto> _productos = new();

    private readonly List<Proveedor> _proveedores = new();

    // Conserva las facturas completas registradas por la API.
    private readonly List<Factura> _facturas = new();

    // Conserva la siguiente secuencia de numero de factura.
    private int _siguienteNumeroFactura = 1;

    // Carga los datos iniciales una vez al crear el almacen.
    public AlmacenCarniceria()
    {
        SembrarDatosIniciales();
    }

    // Ejecuta una operacion compuesta bajo el mismo bloqueo de las colecciones.
    public TResultado EjecutarConBloqueo<TResultado>(Func<TResultado> operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        lock (_sincronizacion)
        {
            return operacion();
        }
    }

    // Devuelve una coleccion de solo lectura con copias independientes.
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

    // Obtiene una copia del cliente que coincide con el Id.
    public Cliente? ObtenerCliente(Guid id)
    {
        lock (_sincronizacion)
        {
            return _clientes
                .FirstOrDefault(cliente => cliente.Id == id)
                ?.Copiar();
        }
    }

    // Busca identificacion exacta y omite consultas vacias.
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

    // Devuelve una coleccion de solo lectura con copias independientes.
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

    // Obtiene una copia del empleado que coincide con el Id.
    public Empleado? ObtenerEmpleado(Guid id)
    {
        lock (_sincronizacion)
        {
            return _empleados
                .FirstOrDefault(empleado => empleado.Id == id)
                ?.Copiar();
        }
    }

    // Busca identificacion exacta y omite consultas vacias.
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

    // Devuelve una coleccion de solo lectura con copias independientes.
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

    // Obtiene una copia del producto que coincide con el Id.
    public Producto? ObtenerProducto(Guid id)
    {
        lock (_sincronizacion)
        {
            return _productos
                .FirstOrDefault(producto => producto.Id == id)
                ?.Copiar();
        }
    }

    // Busca codigos exactos y omite consultas vacias.
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

    // Busca nombres parciales y omite consultas vacias.
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

    // Devuelve una coleccion de solo lectura con copias independientes.
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

    // Obtiene una copia del proveedor que coincide con el Id.
    public Proveedor? ObtenerProveedor(Guid id)
    {
        lock (_sincronizacion)
        {
            return _proveedores
                .FirstOrDefault(proveedor => proveedor.Id == id)
                ?.Copiar();
        }
    }

    // Busca identificacion exacta y omite consultas vacias.
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

    // Busca razon social parcial y omite consultas vacias.
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

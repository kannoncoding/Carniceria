using System;
using System.Collections.Generic;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

public interface IAlmacenCarniceria
{
    // Clientes

    IReadOnlyList<Cliente> ListarClientes();

    Cliente? ObtenerCliente(Guid id);

    IReadOnlyList<Cliente> BuscarClientesPorIdentificacion(
        string identificacion);

    ResultadoOperacion CrearCliente(
        ClienteFormulario formulario);

    ResultadoOperacion ActualizarCliente(
        Guid id,
        ClienteFormulario formulario);

    ResultadoOperacion EliminarCliente(Guid id);

    // Empleados

    IReadOnlyList<Empleado> ListarEmpleados();

    Empleado? ObtenerEmpleado(Guid id);

    IReadOnlyList<Empleado> BuscarEmpleadosPorIdentificacion(
        string identificacion);

    ResultadoOperacion CrearEmpleado(
        EmpleadoFormulario formulario);

    ResultadoOperacion ActualizarEmpleado(
        Guid id,
        EmpleadoFormulario formulario);

    ResultadoOperacion EliminarEmpleado(Guid id);

    // Productos

    IReadOnlyList<Producto> ListarProductos();

    Producto? ObtenerProducto(Guid id);

    IReadOnlyList<Producto> BuscarProductosPorCodigo(
        string codigo);

    IReadOnlyList<Producto> BuscarProductosPorNombre(
        string nombre);

    ResultadoOperacion CrearProducto(
        ProductoFormulario formulario);

    ResultadoOperacion ActualizarProducto(
        Guid id,
        ProductoFormulario formulario);

    ResultadoOperacion EliminarProducto(Guid id);

    // Proveedores

    IReadOnlyList<Proveedor> ListarProveedores();

    Proveedor? ObtenerProveedor(Guid id);

    IReadOnlyList<Proveedor> BuscarProveedoresPorIdentificacion(
        string identificacion);

    IReadOnlyList<Proveedor> BuscarProveedoresPorRazonSocial(
        string razonSocial);

    ResultadoOperacion CrearProveedor(
        ProveedorFormulario formulario);

    ResultadoOperacion ActualizarProveedor(
        Guid id,
        ProveedorFormulario formulario);

    ResultadoOperacion EliminarProveedor(Guid id);
}
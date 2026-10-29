using System;
using System.Collections.Generic;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Web.Data;

// Define las operaciones disponibles para el almacén temporal de MVC.
public interface IAlmacenCarniceria
{
    // Operaciones de consulta y mantenimiento de clientes.

    // Devuelve copias de todos los clientes registrados.
    IReadOnlyList<Cliente> ListarClientes();

    // Devuelve una copia del cliente indicado o null si no existe.
    Cliente? ObtenerCliente(Guid id);

    // Busca clientes por identificación exacta.
    IReadOnlyList<Cliente> BuscarClientesPorIdentificacion(
        string identificacion);

    // Valida y agrega un cliente al almacén.
    ResultadoOperacion CrearCliente(
        ClienteFormulario formulario);

    // Valida y actualiza el cliente indicado.
    ResultadoOperacion ActualizarCliente(
        Guid id,
        ClienteFormulario formulario);

    // Elimina el cliente indicado si existe.
    ResultadoOperacion EliminarCliente(Guid id);

    // Operaciones de consulta y mantenimiento de empleados.

    // Devuelve copias de todos los empleados registrados.
    IReadOnlyList<Empleado> ListarEmpleados();

    // Devuelve una copia del empleado indicado o null si no existe.
    Empleado? ObtenerEmpleado(Guid id);

    // Busca empleados por identificación exacta.
    IReadOnlyList<Empleado> BuscarEmpleadosPorIdentificacion(
        string identificacion);

    // Valida y agrega un empleado al almacén.
    ResultadoOperacion CrearEmpleado(
        EmpleadoFormulario formulario);

    // Valida y actualiza el empleado indicado.
    ResultadoOperacion ActualizarEmpleado(
        Guid id,
        EmpleadoFormulario formulario);

    // Elimina el empleado indicado si existe.
    ResultadoOperacion EliminarEmpleado(Guid id);

    // Operaciones de consulta y mantenimiento de productos.

    // Devuelve copias de todos los productos registrados.
    IReadOnlyList<Producto> ListarProductos();

    // Devuelve una copia del producto indicado o null si no existe.
    Producto? ObtenerProducto(Guid id);

    // Busca productos por código exacto.
    IReadOnlyList<Producto> BuscarProductosPorCodigo(
        string codigo);

    // Busca productos por coincidencia parcial de nombre.
    IReadOnlyList<Producto> BuscarProductosPorNombre(
        string nombre);

    // Valida y agrega un producto al almacén.
    ResultadoOperacion CrearProducto(
        ProductoFormulario formulario);

    // Valida y actualiza el producto indicado.
    ResultadoOperacion ActualizarProducto(
        Guid id,
        ProductoFormulario formulario);

    // Elimina el producto indicado si existe.
    ResultadoOperacion EliminarProducto(Guid id);

    // Operaciones de consulta y mantenimiento de proveedores.

    // Devuelve copias de todos los proveedores registrados.
    IReadOnlyList<Proveedor> ListarProveedores();

    // Devuelve una copia del proveedor indicado o null si no existe.
    Proveedor? ObtenerProveedor(Guid id);

    // Busca proveedores por identificación exacta.
    IReadOnlyList<Proveedor> BuscarProveedoresPorIdentificacion(
        string identificacion);

    // Busca proveedores por coincidencia parcial de razón social.
    IReadOnlyList<Proveedor> BuscarProveedoresPorRazonSocial(
        string razonSocial);

    // Valida y agrega un proveedor al almacén.
    ResultadoOperacion CrearProveedor(
        ProveedorFormulario formulario);

    // Valida y actualiza el proveedor indicado.
    ResultadoOperacion ActualizarProveedor(
        Guid id,
        ProveedorFormulario formulario);

    // Elimina el proveedor indicado si existe.
    ResultadoOperacion EliminarProveedor(Guid id);
}

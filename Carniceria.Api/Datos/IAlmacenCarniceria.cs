using System;
using System.Collections.Generic;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

// Define las consultas y modificaciones disponibles para la API.
public interface IAlmacenCarniceria
{
    // Agrupa una lectura o modificacion compuesta bajo el bloqueo comun.
    TResultado EjecutarConBloqueo<TResultado>(Func<TResultado> operacion);

    // Operaciones de consulta y mantenimiento de clientes.

    // Devuelve copias de todos los clientes registrados.
    IReadOnlyList<Cliente> ListarClientes();

    // Devuelve una copia del cliente indicado o null si no existe.
    Cliente? ObtenerCliente(Guid id);

    // Busca clientes por identificacion exacta.
    IReadOnlyList<Cliente> BuscarClientesPorIdentificacion(
        string identificacion);

    // Valida y agrega un cliente al almacen.
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

    // Busca empleados por identificacion exacta.
    IReadOnlyList<Empleado> BuscarEmpleadosPorIdentificacion(
        string identificacion);

    // Valida y agrega un empleado al almacen.
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

    // Busca productos por codigo exacto.
    IReadOnlyList<Producto> BuscarProductosPorCodigo(
        string codigo);

    // Busca productos por coincidencia parcial de nombre.
    IReadOnlyList<Producto> BuscarProductosPorNombre(
        string nombre);

    // Valida la referencia y agrega un producto al almacen.
    ResultadoOperacion CrearProducto(
        ProductoFormulario formulario);

    // Valida la referencia y actualiza el producto indicado.
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

    // Busca proveedores por identificacion exacta.
    IReadOnlyList<Proveedor> BuscarProveedoresPorIdentificacion(
        string identificacion);

    // Busca proveedores por coincidencia parcial de razon social.
    IReadOnlyList<Proveedor> BuscarProveedoresPorRazonSocial(
        string razonSocial);

    // Valida y agrega un proveedor al almacen.
    ResultadoOperacion CrearProveedor(
        ProveedorFormulario formulario);

    // Valida y actualiza el proveedor indicado.
    ResultadoOperacion ActualizarProveedor(
        Guid id,
        ProveedorFormulario formulario);

    // Rechaza la eliminacion si hay productos relacionados.
    ResultadoOperacion EliminarProveedor(Guid id);

    // Operaciones de consulta y registro de facturas.

    // Devuelve copias de todas las facturas registradas.
    IReadOnlyList<Factura> ListarFacturas();

    // Obtiene una copia de la factura indicada por su Id.
    Factura? ObtenerFactura(Guid id);

    // Obtiene una copia por numero exacto, sin distinguir mayusculas.
    Factura? ObtenerFacturaPorNumero(string numero);

    // Busca numeros exactos de factura.
    IReadOnlyList<Factura> BuscarFacturasPorNumero(string numero);

    // Busca facturas asociadas a la identificacion exacta del cliente.
    IReadOnlyList<Factura> BuscarFacturasPorIdentificacionCliente(
        string identificacion);

    // Valida, calcula, asigna identidad y registra una factura completa.
    ResultadoOperacion CrearFactura(SolicitudFactura solicitud);

    // Valida y calcula una factura sin asignar identidad ni almacenarla.
    (ResultadoOperacion? Error, Factura? Factura) PrevisualizarFactura(
        SolicitudFactura solicitud);

    // Descuenta las existencias y cambia una factura pendiente a pagada.
    ResultadoOperacion PagarFactura(string numero);
}

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Resultados;
using Carniceria.Modelos.Validacion;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    public ResultadoOperacion CrearCliente(
        ClienteFormulario formulario)
    {
        var errores = ValidadorFormularios.Validar(formulario);

        if (errores.Count > 0)
        {
            return ResultadoOperacion.ValidacionFallida(errores);
        }

        lock (_sincronizacion)
        {
            bool identificacionDuplicada = _clientes.Any(cliente =>
                string.Equals(
                    cliente.Identificacion,
                    formulario.Identificacion,
                    StringComparison.OrdinalIgnoreCase));

            if (identificacionDuplicada)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(ClienteFormulario.Identificacion),
                    "Ya existe un cliente con esta identificación.");
            }

            Cliente nuevoCliente;

            do
            {
                nuevoCliente = new Cliente
                {
                    TipoIdentificacion = formulario.TipoIdentificacion!.Value,
                    Identificacion = formulario.Identificacion,
                    Nombre = formulario.Nombre,
                    PrimerApellido = formulario.PrimerApellido,
                    SegundoApellido = formulario.SegundoApellido,
                    FechaNacimiento = formulario.FechaNacimiento!.Value
                };
            }
            while (
                nuevoCliente.Id == Guid.Empty ||
                _clientes.Any(cliente => cliente.Id == nuevoCliente.Id));

            _clientes.Add(nuevoCliente);

            return ResultadoOperacion.Correcto(
                nuevoCliente.Id,
                "Cliente registrado correctamente.");
        }
    }

    public ResultadoOperacion CrearEmpleado(
        EmpleadoFormulario formulario)
    {
        var errores = ValidadorFormularios.Validar(formulario);

        if (errores.Count > 0)
        {
            return ResultadoOperacion.ValidacionFallida(errores);
        }

        lock (_sincronizacion)
        {
            bool identificacionDuplicada = _empleados.Any(empleado =>
                string.Equals(
                    empleado.Identificacion,
                    formulario.Identificacion,
                    StringComparison.OrdinalIgnoreCase));

            if (identificacionDuplicada)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(EmpleadoFormulario.Identificacion),
                    "Ya existe un empleado con esta identificación.");
            }

            Empleado nuevoEmpleado;

            do
            {
                nuevoEmpleado = new Empleado
                {
                    TipoIdentificacion = formulario.TipoIdentificacion!.Value,
                    Identificacion = formulario.Identificacion,
                    Nombre = formulario.Nombre,
                    PrimerApellido = formulario.PrimerApellido,
                    SegundoApellido = formulario.SegundoApellido,
                    FechaNacimiento = formulario.FechaNacimiento!.Value,
                    SalarioMensual = formulario.SalarioMensual!.Value,
                    FechaIngreso = formulario.FechaIngreso!.Value,
                    AreaTrabajo = formulario.AreaTrabajo!.Value,
                    Puesto = formulario.Puesto,
                    Turno = formulario.Turno!.Value
                };
            }
            while (
                nuevoEmpleado.Id == Guid.Empty ||
                _empleados.Any(empleado => empleado.Id == nuevoEmpleado.Id));

            _empleados.Add(nuevoEmpleado);

            return ResultadoOperacion.Correcto(
                nuevoEmpleado.Id,
                "Empleado registrado correctamente.");
        }
    }

    public ResultadoOperacion CrearProducto(
        ProductoFormulario formulario)
    {
        var errores = ValidadorFormularios.Validar(formulario);

        if (errores.Count > 0)
        {
            return ResultadoOperacion.ValidacionFallida(errores);
        }

        lock (_sincronizacion)
        {
            if (!formulario.ProveedorId.HasValue ||
                formulario.ProveedorId.Value == Guid.Empty ||
                !_proveedores.Any(proveedor =>
                    proveedor.Id == formulario.ProveedorId.Value))
            {
                return ResultadoOperacion.ValidacionFallida(
                    new[]
                    {
                        new ValidationResult(
                            "Seleccione un proveedor existente.",
                            new[] { nameof(ProductoFormulario.ProveedorId) })
                    });
            }

            bool codigoDuplicado = _productos.Any(producto =>
                string.Equals(
                    producto.Codigo,
                    formulario.Codigo,
                    StringComparison.OrdinalIgnoreCase));

            if (codigoDuplicado)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(ProductoFormulario.Codigo),
                    "Ya existe un producto con este código.");
            }

            Producto nuevoProducto;

            do
            {
                nuevoProducto = new Producto
                {
                    ProveedorId = formulario.ProveedorId.Value,
                    Codigo = formulario.Codigo,
                    Nombre = formulario.Nombre,
                    Categoria = formulario.Categoria!.Value,
                    UnidadMedida = formulario.UnidadMedida!.Value,
                    PrecioUnidadMedida = formulario.PrecioUnidadMedida!.Value,
                    ExistenciaActual = formulario.ExistenciaActual!.Value,
                    ExistenciaMinima = formulario.ExistenciaMinima!.Value
                };
            }
            while (
                nuevoProducto.Id == Guid.Empty ||
                _productos.Any(producto => producto.Id == nuevoProducto.Id));

            _productos.Add(nuevoProducto);

            return ResultadoOperacion.Correcto(
                nuevoProducto.Id,
                "Producto registrado correctamente.");
        }
    }

    public ResultadoOperacion CrearProveedor(
        ProveedorFormulario formulario)
    {
        var errores = ValidadorFormularios.Validar(formulario);

        if (errores.Count > 0)
        {
            return ResultadoOperacion.ValidacionFallida(errores);
        }

        lock (_sincronizacion)
        {
            bool identificacionDuplicada = _proveedores.Any(proveedor =>
                string.Equals(
                    proveedor.Identificacion,
                    formulario.Identificacion,
                    StringComparison.OrdinalIgnoreCase));

            if (identificacionDuplicada)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(ProveedorFormulario.Identificacion),
                    "Ya existe un proveedor con esta identificación.");
            }

            Proveedor nuevoProveedor;

            do
            {
                nuevoProveedor = new Proveedor
                {
                    TipoIdentificacion = formulario.TipoIdentificacion!.Value,
                    Identificacion = formulario.Identificacion,
                    RazonSocial = formulario.RazonSocial,
                    NombreContacto = formulario.NombreContacto,
                    Telefono = formulario.Telefono,
                    CorreoElectronico = formulario.CorreoElectronico,
                    TipoSuministro = formulario.TipoSuministro!.Value,
                    FrecuenciaEntrega = formulario.FrecuenciaEntrega!.Value
                };
            }
            while (
                nuevoProveedor.Id == Guid.Empty ||
                _proveedores.Any(proveedor => proveedor.Id == nuevoProveedor.Id));

            _proveedores.Add(nuevoProveedor);

            return ResultadoOperacion.Correcto(
                nuevoProveedor.Id,
                "Proveedor registrado correctamente.");
        }
    }
}

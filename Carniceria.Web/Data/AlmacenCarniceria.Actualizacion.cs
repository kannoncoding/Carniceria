using System;
using System.Linq;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Resultados;
using Carniceria.Modelos.Validacion;

namespace Carniceria.Web.Data;

public partial class AlmacenCarniceria
{
    public ResultadoOperacion ActualizarCliente(
        Guid id,
        ClienteFormulario formulario)
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

            var errores = ValidadorFormularios.Validar(formulario);

            if (errores.Count > 0)
            {
                return ResultadoOperacion.ValidacionFallida(errores);
            }

            bool identificacionDuplicada = _clientes.Any(otro =>
                otro.Id != id &&
                string.Equals(
                    otro.Identificacion,
                    formulario.Identificacion,
                    StringComparison.OrdinalIgnoreCase));

            if (identificacionDuplicada)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(ClienteFormulario.Identificacion),
                    "Ya existe otro cliente con esta identificación.");
            }

            cliente.TipoIdentificacion = formulario.TipoIdentificacion!.Value;
            cliente.Identificacion = formulario.Identificacion;
            cliente.Nombre = formulario.Nombre;
            cliente.PrimerApellido = formulario.PrimerApellido;
            cliente.SegundoApellido = formulario.SegundoApellido;
            cliente.FechaNacimiento = formulario.FechaNacimiento!.Value;

            return ResultadoOperacion.Correcto(
                cliente.Id,
                "Cliente actualizado correctamente.");
        }
    }

    public ResultadoOperacion ActualizarEmpleado(
        Guid id,
        EmpleadoFormulario formulario)
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

            var errores = ValidadorFormularios.Validar(formulario);

            if (errores.Count > 0)
            {
                return ResultadoOperacion.ValidacionFallida(errores);
            }

            bool identificacionDuplicada = _empleados.Any(otro =>
                otro.Id != id &&
                string.Equals(
                    otro.Identificacion,
                    formulario.Identificacion,
                    StringComparison.OrdinalIgnoreCase));

            if (identificacionDuplicada)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(EmpleadoFormulario.Identificacion),
                    "Ya existe otro empleado con esta identificación.");
            }

            empleado.TipoIdentificacion = formulario.TipoIdentificacion!.Value;
            empleado.Identificacion = formulario.Identificacion;
            empleado.Nombre = formulario.Nombre;
            empleado.PrimerApellido = formulario.PrimerApellido;
            empleado.SegundoApellido = formulario.SegundoApellido;
            empleado.FechaNacimiento = formulario.FechaNacimiento!.Value;
            empleado.SalarioMensual = formulario.SalarioMensual!.Value;
            empleado.FechaIngreso = formulario.FechaIngreso!.Value;
            empleado.AreaTrabajo = formulario.AreaTrabajo!.Value;
            empleado.Puesto = formulario.Puesto;
            empleado.Turno = formulario.Turno!.Value;

            return ResultadoOperacion.Correcto(
                empleado.Id,
                "Empleado actualizado correctamente.");
        }
    }

    public ResultadoOperacion ActualizarProducto(
        Guid id,
        ProductoFormulario formulario)
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

            var errores = ValidadorFormularios.Validar(formulario);

            if (errores.Count > 0)
            {
                return ResultadoOperacion.ValidacionFallida(errores);
            }

            bool codigoDuplicado = _productos.Any(otro =>
                otro.Id != id &&
                string.Equals(
                    otro.Codigo,
                    formulario.Codigo,
                    StringComparison.OrdinalIgnoreCase));

            if (codigoDuplicado)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(ProductoFormulario.Codigo),
                    "Ya existe otro producto con este código.");
            }

            producto.Codigo = formulario.Codigo;
            producto.Nombre = formulario.Nombre;
            producto.Categoria = formulario.Categoria!.Value;
            producto.UnidadMedida = formulario.UnidadMedida!.Value;
            producto.PrecioUnidadMedida = formulario.PrecioUnidadMedida!.Value;
            producto.ExistenciaActual = formulario.ExistenciaActual!.Value;
            producto.ExistenciaMinima = formulario.ExistenciaMinima!.Value;

            return ResultadoOperacion.Correcto(
                producto.Id,
                "Producto actualizado correctamente.");
        }
    }

    public ResultadoOperacion ActualizarProveedor(
        Guid id,
        ProveedorFormulario formulario)
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

            var errores = ValidadorFormularios.Validar(formulario);

            if (errores.Count > 0)
            {
                return ResultadoOperacion.ValidacionFallida(errores);
            }

            bool identificacionDuplicada = _proveedores.Any(otro =>
                otro.Id != id &&
                string.Equals(
                    otro.Identificacion,
                    formulario.Identificacion,
                    StringComparison.OrdinalIgnoreCase));

            if (identificacionDuplicada)
            {
                return ResultadoOperacion.Duplicado(
                    nameof(ProveedorFormulario.Identificacion),
                    "Ya existe otro proveedor con esta identificación.");
            }

            proveedor.TipoIdentificacion = formulario.TipoIdentificacion!.Value;
            proveedor.Identificacion = formulario.Identificacion;
            proveedor.RazonSocial = formulario.RazonSocial;
            proveedor.NombreContacto = formulario.NombreContacto;
            proveedor.Telefono = formulario.Telefono;
            proveedor.CorreoElectronico = formulario.CorreoElectronico;
            proveedor.TipoSuministro = formulario.TipoSuministro!.Value;
            proveedor.FrecuenciaEntrega = formulario.FrecuenciaEntrega!.Value;

            return ResultadoOperacion.Correcto(
                proveedor.Id,
                "Proveedor actualizado correctamente.");
        }
    }
}
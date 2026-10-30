using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Entidades;

namespace Carniceria.Api.Mapeos;

// Proyecta entidades del almacen a contratos que no exponen su estado interno.
public static class MapeadorRespuestas
{
    // Copia los datos publicos del cliente a su contrato de respuesta.
    public static RespuestaCliente Convertir(Cliente cliente)
    {
        return new RespuestaCliente
        {
            Id = cliente.Id,
            TipoIdentificacion = cliente.TipoIdentificacion,
            Identificacion = cliente.Identificacion,
            Nombre = cliente.Nombre,
            PrimerApellido = cliente.PrimerApellido,
            SegundoApellido = cliente.SegundoApellido,
            FechaNacimiento = cliente.FechaNacimiento
        };
    }

    // Copia los datos personales y laborales del empleado.
    public static RespuestaEmpleado Convertir(Empleado empleado)
    {
        return new RespuestaEmpleado
        {
            Id = empleado.Id,
            TipoIdentificacion = empleado.TipoIdentificacion,
            Identificacion = empleado.Identificacion,
            Nombre = empleado.Nombre,
            PrimerApellido = empleado.PrimerApellido,
            SegundoApellido = empleado.SegundoApellido,
            FechaNacimiento = empleado.FechaNacimiento,
            SalarioMensual = empleado.SalarioMensual,
            FechaIngreso = empleado.FechaIngreso,
            AreaTrabajo = empleado.AreaTrabajo,
            Puesto = empleado.Puesto,
            Turno = empleado.Turno
        };
    }

    // Incluye los datos actuales del proveedor relacionado con el producto.
    public static RespuestaProducto Convertir(
        Producto producto,
        Proveedor proveedor)
    {
        if (producto.ProveedorId != proveedor.Id)
        {
            throw new ArgumentException(
                "El proveedor indicado no corresponde al producto.",
                nameof(proveedor));
        }

        return new RespuestaProducto
        {
            Id = producto.Id,
            ProveedorId = producto.ProveedorId,
            ProveedorRazonSocial = proveedor.RazonSocial,
            ProveedorIdentificacion = proveedor.Identificacion,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Categoria = producto.Categoria,
            UnidadMedida = producto.UnidadMedida,
            PrecioUnidadMedida = producto.PrecioUnidadMedida,
            ExistenciaActual = producto.ExistenciaActual,
            ExistenciaMinima = producto.ExistenciaMinima,
            TieneExistenciaBaja = producto.TieneExistenciaBaja
        };
    }

    // Copia los datos comerciales y de contacto del proveedor.
    public static RespuestaProveedor Convertir(Proveedor proveedor)
    {
        return new RespuestaProveedor
        {
            Id = proveedor.Id,
            TipoIdentificacion = proveedor.TipoIdentificacion,
            Identificacion = proveedor.Identificacion,
            RazonSocial = proveedor.RazonSocial,
            NombreContacto = proveedor.NombreContacto,
            Telefono = proveedor.Telefono,
            CorreoElectronico = proveedor.CorreoElectronico,
            TipoSuministro = proveedor.TipoSuministro,
            FrecuenciaEntrega = proveedor.FrecuenciaEntrega
        };
    }
}

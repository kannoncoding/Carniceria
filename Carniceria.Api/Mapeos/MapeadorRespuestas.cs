using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Entidades;

namespace Carniceria.Api.Mapeos;

public static class MapeadorRespuestas
{
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

    public static RespuestaProducto Convertir(Producto producto)
    {
        return new RespuestaProducto
        {
            Id = producto.Id,
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

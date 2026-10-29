using System;
using Carniceria.Modelos.Entidades;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Formularios;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Datos;

public partial class AlmacenCarniceria
{
    // Crea registros iniciales usando las mismas reglas de alta del almacén.
    private void SembrarDatosIniciales()
    {
        var resultadoCliente = CrearCliente(new ClienteFormulario
        {
            TipoIdentificacion = TipoIdentificacion.CedulaNacional,
            Identificacion = "1-1111-1111",
            Nombre = "Ana",
            PrimerApellido = "Mora",
            SegundoApellido = "Rojas",
            FechaNacimiento = new DateTime(1990, 1, 15)
        });
        ObtenerIdentificadorSemilla(resultadoCliente, "el cliente Ana");

        var resultadoEmpleado = CrearEmpleado(new EmpleadoFormulario
        {
            TipoIdentificacion = TipoIdentificacion.CedulaNacional,
            Identificacion = "2-2222-2222",
            Nombre = "Luis",
            PrimerApellido = "Vega",
            SegundoApellido = "Solis",
            FechaNacimiento = new DateTime(1985, 6, 20),
            SalarioMensual = 600000m,
            FechaIngreso = new DateTime(2020, 1, 6),
            AreaTrabajo = AreaTrabajo.VentasYCaja,
            Puesto = "Cajero",
            Turno = Turno.Manana
        });
        ObtenerIdentificadorSemilla(resultadoEmpleado, "el empleado Luis");

        var resultadoProveedor = CrearProveedor(new ProveedorFormulario
        {
            TipoIdentificacion = TipoIdentificacion.CedulaJuridica,
            Identificacion = "3-101-123456",
            RazonSocial = "CarnesDelValle",
            NombreContacto = "Mario Rojas",
            Telefono = "22223333",
            CorreoElectronico = "contacto@example.com",
            TipoSuministro = TipoSuministro.Otros,
            FrecuenciaEntrega = FrecuenciaEntrega.Semanal
        });
        var proveedorId = ObtenerIdentificadorSemilla(
            resultadoProveedor,
            "el proveedor CarnesDelValle");

        var resultadoCarne = CrearProducto(new ProductoFormulario
        {
            ProveedorId = proveedorId,
            Codigo = "RES001",
            Nombre = "ResMolida",
            Categoria = CategoriaProducto.CarneDeRes,
            UnidadMedida = UnidadMedida.Kilogramo,
            PrecioUnidadMedida = 4000m,
            ExistenciaActual = 10m,
            ExistenciaMinima = 2m
        });
        ObtenerIdentificadorSemilla(resultadoCarne, "el producto RES001");

        var resultadoPollo = CrearProducto(new ProductoFormulario
        {
            ProveedorId = proveedorId,
            Codigo = "POL001",
            Nombre = "PolloEntero",
            Categoria = CategoriaProducto.Pollo,
            UnidadMedida = UnidadMedida.Kilogramo,
            PrecioUnidadMedida = 3000m,
            ExistenciaActual = 10m,
            ExistenciaMinima = 2m
        });
        ObtenerIdentificadorSemilla(resultadoPollo, "el producto POL001");
    }

    // Falla el inicio si una semilla no se creó con un Id válido.
    private static Guid ObtenerIdentificadorSemilla(
        ResultadoOperacion resultado,
        string nombreRegistro)
    {
        if (!resultado.EsExitoso ||
            !resultado.Id.HasValue ||
            resultado.Id.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"No se pudo crear {nombreRegistro} para los datos iniciales: {resultado.Mensaje}");
        }

        return resultado.Id.Value;
    }
}

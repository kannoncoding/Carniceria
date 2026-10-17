using System.Collections.Generic;
using Carniceria.Modelos.Entidades;

namespace Carniceria.Web.Data;

public class AlmacenCarniceria
{
    private readonly List<Cliente> _clientes = new();

    private readonly List<Empleado> _empleados = new();

    private readonly List<Producto> _productos = new();

    private readonly List<Proveedor> _proveedores = new();
}
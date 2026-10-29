using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

// Respuesta API de inventario con datos descriptivos del proveedor.
public sealed class RespuestaProducto
{
    // Identificador del producto y proveedor asociado.
    public Guid Id { get; init; }

    public Guid ProveedorId { get; init; }

    public string ProveedorRazonSocial { get; init; } = string.Empty;

    public string ProveedorIdentificacion { get; init; } = string.Empty;

    // Datos usados para identificar y clasificar el producto.
    public string Codigo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public CategoriaProducto Categoria { get; init; }

    public UnidadMedida UnidadMedida { get; init; }

    // Precio, existencias y estado calculado de inventario.
    public decimal PrecioUnidadMedida { get; init; }

    public decimal ExistenciaActual { get; init; }

    public decimal ExistenciaMinima { get; init; }

    public bool TieneExistenciaBaja { get; init; }
}

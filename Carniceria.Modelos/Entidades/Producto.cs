using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

// Representa un articulo del inventario relacionado con un proveedor.
public class Producto
{
    // Identificador interno asignado al crear el producto.
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Proveedor y datos usados para identificar y clasificar el producto.
    public Guid ProveedorId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public CategoriaProducto Categoria { get; set; }

    public UnidadMedida UnidadMedida { get; set; }

    // Precio y existencias expresados en la unidad de medida del producto.
    public decimal PrecioUnidadMedida { get; set; }

    public decimal ExistenciaActual { get; set; }

    public decimal ExistenciaMinima { get; set; }

    // Indica si la existencia llego al minimo configurado.
    public bool TieneExistenciaBaja => ExistenciaActual <= ExistenciaMinima;

    // Devuelve una copia independiente de los datos simples de la entidad.
    public Producto Copiar()
    {
        return (Producto)MemberwiseClone();
    }
}

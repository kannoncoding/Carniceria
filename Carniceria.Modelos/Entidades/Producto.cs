using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

public class Producto
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public CategoriaProducto Categoria { get; set; }

    public UnidadMedida UnidadMedida { get; set; }

    public decimal PrecioUnidadMedida { get; set; }

    public decimal ExistenciaActual { get; set; }

    public decimal ExistenciaMinima { get; set; }

    public bool TieneExistenciaBaja => ExistenciaActual <= ExistenciaMinima;

    public Producto Copiar()
    {
        return (Producto)MemberwiseClone();
    }
}
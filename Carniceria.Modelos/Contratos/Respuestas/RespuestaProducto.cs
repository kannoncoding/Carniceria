using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Contratos.Respuestas;

public sealed class RespuestaProducto
{
    public Guid Id { get; init; }

    public Guid ProveedorId { get; init; }

    public string ProveedorRazonSocial { get; init; } = string.Empty;

    public string ProveedorIdentificacion { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public CategoriaProducto Categoria { get; init; }

    public UnidadMedida UnidadMedida { get; init; }

    public decimal PrecioUnidadMedida { get; init; }

    public decimal ExistenciaActual { get; init; }

    public decimal ExistenciaMinima { get; init; }

    public bool TieneExistenciaBaja { get; init; }
}

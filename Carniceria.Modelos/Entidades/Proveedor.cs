using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

// Representa una entidad que suministra productos.
public class Proveedor
{
    // Identificador interno asignado al crear el proveedor.
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Datos legales y comerciales del proveedor.
    public TipoIdentificacion TipoIdentificacion { get; set; }

    public string Identificacion { get; set; } = string.Empty;

    public string RazonSocial { get; set; } = string.Empty;

    public string NombreContacto { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string CorreoElectronico { get; set; } = string.Empty;

    // Clasificacion del suministro y frecuencia de entrega.
    public TipoSuministro TipoSuministro { get; set; }

    public FrecuenciaEntrega FrecuenciaEntrega { get; set; }

    // Devuelve una copia independiente de los datos simples de la entidad.
    public Proveedor Copiar()
    {
        return (Proveedor)MemberwiseClone();
    }
}

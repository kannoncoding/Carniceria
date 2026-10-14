using System;
using Carniceria.Modelos.Enumeraciones;

namespace Carniceria.Modelos.Entidades;

public class Proveedor
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public TipoIdentificacion TipoIdentificacion { get; set; }

    public string Identificacion { get; set; } = string.Empty;

    public string RazonSocial { get; set; } = string.Empty;

    public string NombreContacto { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string CorreoElectronico { get; set; } = string.Empty;

    public TipoSuministro TipoSuministro { get; set; }

    public FrecuenciaEntrega FrecuenciaEntrega { get; set; }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Consultas;

public class BusquedaViewModel<T>
{
    [Display(Name = "Criterio de búsqueda")]
    public string Criterio { get; set; } = string.Empty;

    [Display(Name = "Texto de búsqueda")]
    public string Consulta { get; set; } = string.Empty;

    public bool BusquedaRealizada { get; set; }

    public IReadOnlyList<T> Resultados { get; set; } = Array.Empty<T>();
}
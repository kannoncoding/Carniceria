using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Consultas;

// Estado y resultados de busqueda de una pagina MVC.
public class BusquedaViewModel<T>
{
    // Criterio elegido y texto que se consulta en el modulo.
    [Display(Name = "Criterio de búsqueda")]
    public string Criterio { get; set; } = string.Empty;

    [Display(Name = "Texto de búsqueda")]
    public string Consulta { get; set; } = string.Empty;

    // Distingue el formulario inicial de una consulta ya enviada.
    public bool BusquedaRealizada { get; set; }

    // Resultados visibles para el tipo de entidad de la pagina.
    public IReadOnlyList<T> Resultados { get; set; } = Array.Empty<T>();
}

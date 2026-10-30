using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

// Categorias comerciales disponibles para clasificar productos.
public enum CategoriaProducto
{
    [Display(Name = "Carne de Res")]
    CarneDeRes = 1,

    [Display(Name = "Carne de Cerdo")]
    CarneDeCerdo = 2,

    [Display(Name = "Pollo")]
    Pollo = 3,

    [Display(Name = "Embutidos")]
    Embutidos = 4,

    [Display(Name = "Productos Procesados")]
    ProductosProcesados = 5,

    [Display(Name = "Congelados")]
    Congelados = 6,

    [Display(Name = "Otros")]
    Otros = 7
}

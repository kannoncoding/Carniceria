using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

// Clases de bienes que puede suministrar un proveedor.
public enum TipoSuministro
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

    [Display(Name = "Empaques")]
    Empaques = 6,

    [Display(Name = "Condimentos")]
    Condimentos = 7,

    [Display(Name = "Otros")]
    Otros = 8
}

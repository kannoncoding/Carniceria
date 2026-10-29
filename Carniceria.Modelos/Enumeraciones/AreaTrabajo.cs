using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

// Áreas operativas disponibles para asignar a un empleado.
public enum AreaTrabajo
{
    [Display(Name = "Administración")]
    Administracion = 1,

    [Display(Name = "Ventas y Caja")]
    VentasYCaja = 2,

    [Display(Name = "Corte y Preparación")]
    CorteYPreparacion = 3,

    [Display(Name = "Bodega e Inventario")]
    BodegaEInventario = 4,

    [Display(Name = "Distribución")]
    Distribucion = 5,

    [Display(Name = "Limpieza")]
    Limpieza = 6
}

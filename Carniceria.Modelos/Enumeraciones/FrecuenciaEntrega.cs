using System.ComponentModel.DataAnnotations;

namespace Carniceria.Modelos.Enumeraciones;

public enum FrecuenciaEntrega
{
    [Display(Name = "Diaria")]
    Diaria = 1,

    [Display(Name = "Dos veces por semana")]
    DosVecesPorSemana = 2,

    [Display(Name = "Semanal")]
    Semanal = 3,

    [Display(Name = "Quincenal")]
    Quincenal = 4,

    [Display(Name = "Contra pedido")]
    ContraPedido = 5
}
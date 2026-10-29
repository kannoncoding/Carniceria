namespace Carniceria.Api.Reglas;

// Valores centrales que comparten la validación y la configuración de facturas.
public static class ReglasFacturacion
{
    // Porcentaje máximo de descuento admitido por la API.
    public const decimal PorcentajeDescuentoMaximo = 25m;

    // Porcentaje de IVA aplicado por el calculador de facturas.
    public const decimal PorcentajeIva = 13m;
}

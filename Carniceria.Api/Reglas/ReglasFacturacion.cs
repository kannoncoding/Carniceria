namespace Carniceria.Api.Reglas;

// Valores centrales que comparten la validacion y la configuracion de facturas.
public static class ReglasFacturacion
{
    // Porcentaje maximo de descuento admitido por la API.
    public const decimal PorcentajeDescuentoMaximo = 25m;

    // Porcentaje de IVA aplicado por el calculador de facturas.
    public const decimal PorcentajeIva = 13m;
}

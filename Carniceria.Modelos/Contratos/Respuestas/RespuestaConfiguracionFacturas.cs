namespace Carniceria.Modelos.Contratos.Respuestas;

// Porcentajes numéricos que la API usa al validar y calcular facturas.
public sealed class RespuestaConfiguracionFacturas
{
    // Máximo de descuento permitido para una solicitud.
    public decimal PorcentajeDescuentoMaximo { get; init; }

    // Porcentaje de IVA aplicado a la base gravable.
    public decimal PorcentajeIva { get; init; }
}

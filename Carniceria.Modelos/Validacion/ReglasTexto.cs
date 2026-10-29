namespace Carniceria.Modelos.Validacion;

// Normaliza texto y expone el patrón alfanumérico compartido.
public static class ReglasTexto
{
    // Admite letras españolas y números, sin espacios ni signos.
    public const string PatronAlfanumerico =
        @"\A[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9]+\z";

    // Recorta extremos y convierte valores nulos en texto vacío.
    public static string Normalizar(string? valor)
    {
        return valor?.Trim() ?? string.Empty;
    }
}

namespace Carniceria.Modelos.Validacion;

// Normaliza texto y expone el patron alfanumerico compartido.
public static class ReglasTexto
{
    // Admite letras españolas y numeros, sin espacios ni signos.
    public const string PatronAlfanumerico =
        @"\A[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9]+\z";

    // Recorta extremos y convierte valores nulos en texto vacio.
    public static string Normalizar(string? valor)
    {
        return valor?.Trim() ?? string.Empty;
    }
}

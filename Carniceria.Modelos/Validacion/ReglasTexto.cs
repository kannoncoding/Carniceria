namespace Carniceria.Modelos.Validacion;

public static class ReglasTexto
{
    public const string PatronAlfanumerico =
        @"\A[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9]+\z";

    public static string Normalizar(string? valor)
    {
        return valor?.Trim() ?? string.Empty;
    }
}
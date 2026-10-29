namespace Carniceria.Modelos.Entidades;

// Representa una persona registrada como cliente.
public class Cliente : Persona
{
    // Devuelve una copia independiente de los datos simples de la entidad.
    public Cliente Copiar()
    {
        return (Cliente)MemberwiseClone();
    }
}

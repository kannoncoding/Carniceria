namespace Carniceria.Modelos.Entidades;

public class Cliente : Persona
{
    public Cliente Copiar()
    {
        return (Cliente)MemberwiseClone();
    }
}
using TipoDocumento = Carniceria.Modelos.Enumeraciones.TipoIdentificacion;

namespace Carniceria.Modelos.Formularios;

public class ClienteFormulario : PersonaFormulario
{
    protected override TipoDocumento[] TiposPermitidos =>
        new[]
        {
            TipoDocumento.CedulaNacional,
            TipoDocumento.Dimex,
            TipoDocumento.Pasaporte
        };
}
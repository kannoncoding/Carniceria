using TipoDocumento = Carniceria.Modelos.Enumeraciones.TipoIdentificacion;

namespace Carniceria.Modelos.Formularios;

// Valida personas registradas como clientes.
public class ClienteFormulario : PersonaFormulario
{
    // Los clientes pueden registrarse con cédula nacional, DIMEX o pasaporte.
    protected override TipoDocumento[] TiposPermitidos =>
        new[]
        {
            TipoDocumento.CedulaNacional,
            TipoDocumento.Dimex,
            TipoDocumento.Pasaporte
        };
}

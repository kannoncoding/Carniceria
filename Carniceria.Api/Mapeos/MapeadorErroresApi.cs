using System.Collections.Generic;
using System.Linq;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Modelos.Resultados;

namespace Carniceria.Api.Mapeos;

// Convierte resultados del almacén al contrato común de errores HTTP.
public static class MapeadorErroresApi
{
    // Agrupa mensajes por campo y asigna un código estable al resultado.
    public static ErrorApi Convertir(ResultadoOperacion resultado)
    {
        // Acumula mensajes para combinar errores que comparten campo.
        var erroresPorCampo = new Dictionary<string, List<string>>();

        foreach (var error in resultado.Errores)
        {
            var campos = error.MemberNames.Any()
                ? error.MemberNames
                : new[] { string.Empty };

            foreach (var campo in campos)
            {
                if (!erroresPorCampo.TryGetValue(campo, out var mensajes))
                {
                    mensajes = new List<string>();
                    erroresPorCampo[campo] = mensajes;
                }

                mensajes.Add(error.ErrorMessage ?? resultado.Mensaje);
            }
        }

        return new ErrorApi
        {
            Codigo = resultado.Estado switch
            {
                EstadoOperacion.ValidacionFallida => "ValidacionFallida",
                EstadoOperacion.Duplicado => "Duplicado",
                EstadoOperacion.NoEncontrado => "NoEncontrado",
                EstadoOperacion.ConflictoIntegridad => "ConflictoIntegridad",
                _ => "Exito"
            },
            Mensaje = resultado.Mensaje,
            ErroresPorCampo = erroresPorCampo.ToDictionary(
                par => par.Key,
                par => par.Value.Distinct().ToArray())
        };
    }
}

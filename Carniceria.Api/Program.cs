using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Modelos.Contratos.Respuestas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// Registra los controladores, el formato de errores y el almacen compartido.
var constructor = WebApplication.CreateBuilder(args);

constructor.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
    {
        opciones.SuppressMapClientErrors = true;
        opciones.InvalidModelStateResponseFactory = contexto =>
        {
            // Conserva los errores de enlace agrupados por nombre de campo.
            var erroresPorCampo = contexto.ModelState
                .Where(entrada => entrada.Value?.Errors.Count > 0)
                .ToDictionary(
                    entrada => entrada.Key,
                    entrada => entrada.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "El valor enviado tiene un formato inválido."
                            : error.ErrorMessage)
                        .Distinct()
                        .ToArray(),
                    System.StringComparer.OrdinalIgnoreCase);

            return new BadRequestObjectResult(new ErrorApi
            {
                Codigo = "SolicitudInvalida",
                Mensaje = "Revise los datos enviados.",
                ErroresPorCampo = erroresPorCampo
            });
        };
    });
// Mantiene un unico almacen para las solicitudes de la API.
constructor.Services.AddSingleton<IAlmacenCarniceria, AlmacenCarniceria>();

// Construye la aplicacion y materializa las semillas al iniciar.
var aplicacion = constructor.Build();

_ = aplicacion.Services.GetRequiredService<IAlmacenCarniceria>();

// Completa respuestas HTTP de error que todavia no tienen contenido.
aplicacion.UseStatusCodePages(async contexto =>
{
    var respuesta = contexto.HttpContext.Response;
    var error = respuesta.StatusCode switch
    {
        StatusCodes.Status400BadRequest => new ErrorApi
        {
            Codigo = "SolicitudInvalida",
            Mensaje = "Revise los datos enviados."
        },
        StatusCodes.Status404NotFound => new ErrorApi
        {
            Codigo = "NoEncontrado",
            Mensaje = "La ruta o el recurso solicitado no existe."
        },
        StatusCodes.Status405MethodNotAllowed => new ErrorApi
        {
            Codigo = "MetodoNoPermitido",
            Mensaje = "El método HTTP no está permitido para esta ruta."
        },
        StatusCodes.Status415UnsupportedMediaType => new ErrorApi
        {
            Codigo = "TipoContenidoNoAdmitido",
            Mensaje = "El tipo de contenido enviado no está admitido."
        },
        _ => new ErrorApi
        {
            Codigo = "ErrorHttp",
            Mensaje = "No se pudo completar la solicitud."
        }
    };

    await respuesta.WriteAsJsonAsync(error);
});

aplicacion.UseAuthorization();

aplicacion.MapControllers();

aplicacion.Run();

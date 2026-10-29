using System.Linq;
using Carniceria.Api.Datos;
using Carniceria.Modelos.Contratos.Respuestas;
using Microsoft.AspNetCore.Mvc;

var constructor = WebApplication.CreateBuilder(args);

constructor.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
    {
        opciones.InvalidModelStateResponseFactory = contexto =>
        {
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
constructor.Services.AddSingleton<IAlmacenCarniceria, AlmacenCarniceria>();

var aplicacion = constructor.Build();

_ = aplicacion.Services.GetRequiredService<IAlmacenCarniceria>();

aplicacion.UseAuthorization();

aplicacion.MapControllers();

aplicacion.Run();

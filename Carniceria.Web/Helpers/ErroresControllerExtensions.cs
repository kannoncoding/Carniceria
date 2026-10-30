using System;
using Carniceria.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Carniceria.Web.Helpers;

// Genera una pagina 404 con retorno al modulo asociado a la ruta.
public static class ErroresControllerExtensions
{
    // Prepara el modelo y la vista de recurso no encontrado.
    public static ViewResult RegistroNoEncontrado(
        this Controller controlador,
        string? mensaje = null)
    {
        ArgumentNullException.ThrowIfNull(controlador);

        string? modulo = controlador
            .RouteData
            .Values["controller"]?
            .ToString()?
            .ToLowerInvariant();

        var (controladorRegreso, textoRegreso) = modulo switch
        {
            "clientes" =>
                ("Clientes", "Volver a Clientes"),

            "empleados" =>
                ("Empleados", "Volver a Empleados"),

            "productos" =>
                ("Productos", "Volver al Inventario de Productos"),

            "proveedores" =>
                ("Proveedores", "Volver a Proveedores"),

            _ =>
                ("Home", "Volver al inicio")
        };

        var modelo = new ErrorViewModel
        {
            Titulo = "Página o registro no encontrado",

            Mensaje = string.IsNullOrWhiteSpace(mensaje)
                ? "El registro solicitado no existe o ya no está disponible."
                : mensaje,

            ControladorRegreso = controladorRegreso,
            TextoRegreso = textoRegreso
        };

        var vista = controlador.View(
            "~/Views/Shared/Error.cshtml",
            modelo);

        vista.StatusCode = StatusCodes.Status404NotFound;

        return vista;
    }
}

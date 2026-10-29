using System;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Carniceria.Web.ModelBinding;

// Selecciona el binder que interpreta decimales con cultura es-CR.
public sealed class DecimalModelBinderProvider : IModelBinderProvider
{
    // Acepta espacios externos, signo y separador decimal cultural.
    private const NumberStyles EstilosPermitidos =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;

    // Devuelve el binder solo para valores decimal y decimal nullable.
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // El tipo subyacente permite cubrir decimal y decimal nullable.
        if (context.Metadata.UnderlyingOrModelType != typeof(decimal))
        {
            return null;
        }

        var loggerFactory =
            context.Services.GetRequiredService<ILoggerFactory>();

        return new DecimalModelBinder(
            EstilosPermitidos,
            loggerFactory);
    }
}

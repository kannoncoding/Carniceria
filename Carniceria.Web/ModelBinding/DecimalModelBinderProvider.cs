using System;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Carniceria.Web.ModelBinding;

public sealed class DecimalModelBinderProvider : IModelBinderProvider
{
    private const NumberStyles EstilosPermitidos =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // También reconoce decimal?, usado por nuestros formularios.
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
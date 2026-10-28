using System.Globalization;
using Carniceria.Web.Data;
using Carniceria.Web.ModelBinding;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Usar una cultura fija para todas las solicitudes de la aplicación.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultura = new CultureInfo(
        "es-CR",
        useUserOverride: false);

    // Establecer explícitamente la coma como separador decimal.
    cultura.NumberFormat.NumberDecimalSeparator = ",";
    cultura.NumberFormat.CurrencyDecimalSeparator = ",";

    options.DefaultRequestCulture = new RequestCulture(cultura);

    options.SupportedCultures = new[] { cultura };
    options.SupportedUICultures = new[] { cultura };

    // La cultura de la aplicación no cambia según el navegador,
    // las cookies de idioma ni los parámetros de la dirección.
    options.RequestCultureProviders.Clear();

    // Incluir el idioma de la respuesta en sus encabezados HTTP.
    options.ApplyCurrentCultureToResponseHeaders = true;
});

// Registrar MVC y proteger las solicitudes que pueden modificar datos.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    // Aplicar nuestra configuración antes del proveedor decimal predeterminado.
    options.ModelBinderProviders.Insert(
        0,
        new DecimalModelBinderProvider());

    // Mensajes para errores de conversión y enlace de los datos enviados.
    var mensajes = options.ModelBindingMessageProvider;

    mensajes.SetAttemptedValueIsInvalidAccessor(
        (valor, campo) =>
            $"El valor '{valor}' no es válido para el campo {campo}.");

    mensajes.SetNonPropertyAttemptedValueIsInvalidAccessor(
        valor =>
            $"El valor '{valor}' no es válido.");

    mensajes.SetUnknownValueIsInvalidAccessor(
        campo =>
            $"El valor ingresado no es válido para el campo {campo}.");

    mensajes.SetNonPropertyUnknownValueIsInvalidAccessor(
        () =>
            "El valor ingresado no es válido.");

    mensajes.SetValueIsInvalidAccessor(
        valor =>
            $"El valor '{valor}' no es válido.");

    mensajes.SetValueMustNotBeNullAccessor(
        _ =>
            "Este campo es obligatorio.");

    mensajes.SetValueMustBeANumberAccessor(
        campo =>
            $"El campo {campo} debe contener un número válido.");

    mensajes.SetNonPropertyValueMustBeANumberAccessor(
        () =>
            "Debe ingresar un número válido.");

    mensajes.SetMissingBindRequiredValueAccessor(
        campo =>
            $"Debe proporcionar un valor para el campo {campo}.");

    mensajes.SetMissingKeyOrValueAccessor(
        () =>
            "Debe proporcionar tanto la clave como el valor.");

    mensajes.SetMissingRequestBodyRequiredValueAccessor(
        () =>
            "La solicitud debe incluir los datos requeridos.");
});

// Compartir una instancia del almacén mediante su interfaz.
builder.Services.AddSingleton<IAlmacenCarniceria, AlmacenCarniceria>();

var app = builder.Build();

// Configurar el manejo de errores fuera del entorno de desarrollo.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Inicio/Error");
}

// Establecer la cultura antes de procesar los controladores.
app.UseRequestLocalization();

// Mostrar los errores HTTP mediante una página de la aplicación.
app.UseStatusCodePagesWithReExecute("/Inicio/EstadoHttp/{0}");

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Página inicial mediante la dirección raíz.
app.MapControllerRoute(
    name: "raiz",
    pattern: "",
    defaults: new
    {
        controller = "Home",
        action = "Index"
    })
    .WithStaticAssets();

// Página inicial mediante una dirección en español.
app.MapControllerRoute(
    name: "inicio",
    pattern: "Inicio",
    defaults: new
    {
        controller = "Home",
        action = "Index"
    })
    .WithStaticAssets();

// Páginas de error de la aplicación.
app.MapControllerRoute(
    name: "errores",
    pattern: "Inicio/{action}/{id?}",
    defaults: new
    {
        controller = "Home"
    },
    constraints: new
    {
        action = "^(Error|EstadoHttp)$"
    })
    .WithStaticAssets();

// Limitar las rutas de módulos a los cuatro controladores del proyecto.
const string controladoresModulos =
    "^(Clientes|Empleados|Productos|Proveedores)$";

// Listados: /Clientes, /Empleados, /Productos y /Proveedores.
app.MapControllerRoute(
    name: "listados",
    pattern: "{controller}",
    defaults: new
    {
        action = "Index"
    },
    constraints: new
    {
        controller = controladoresModulos
    })
    .WithStaticAssets();

// Operaciones de los módulos mediante nombres en español.
app.MapControllerRoute(
    name: "operaciones",
    pattern: "{controller}/{action}/{id?}",
    constraints: new
    {
        controller = controladoresModulos,
        action = "^(Detalles|Crear|Buscar|Editar|Eliminar|EliminarConfirmado)$"
    })
    .WithStaticAssets();

app.Run();

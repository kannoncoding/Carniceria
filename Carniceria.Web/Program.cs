using System.Globalization;
using Carniceria.Web.ModelBinding;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

// Registra MVC, servicios compartidos y configuracion de solicitudes.
var constructor = WebApplication.CreateBuilder(args);

// Usar una cultura fija para todas las solicitudes de la aplicacion.
constructor.Services.Configure<RequestLocalizationOptions>(opciones =>
{
    var cultura = new CultureInfo(
        "es-CR",
        useUserOverride: false);

    // Establecer explicitamente la coma como separador decimal.
    cultura.NumberFormat.NumberDecimalSeparator = ",";
    cultura.NumberFormat.CurrencyDecimalSeparator = ",";

    opciones.DefaultRequestCulture = new RequestCulture(cultura);

    opciones.SupportedCultures = new[] { cultura };
    opciones.SupportedUICultures = new[] { cultura };

    // La cultura de la aplicacion no cambia segun el navegador,
    // las cookies de idioma ni los parametros de la direccion.
    opciones.RequestCultureProviders.Clear();

    // Incluir el idioma de la respuesta en sus encabezados HTTP.
    opciones.ApplyCurrentCultureToResponseHeaders = true;
});

// Registrar MVC y proteger las solicitudes que pueden modificar datos.
constructor.Services.AddControllersWithViews(opciones =>
{
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    // Aplicar la configuracion decimal antes del proveedor predeterminado.
    opciones.ModelBinderProviders.Insert(
        0,
        new DecimalModelBinderProvider());

    // Mensajes para errores de conversion y enlace de los datos enviados.
    var mensajes = opciones.ModelBindingMessageProvider;

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

// Configura el cliente nombrado para consultar la API.
constructor.Services.AddHttpClient("ApiCarniceria", cliente =>
{
    var direccionBase = constructor.Configuration["Api:DireccionBase"];
    cliente.BaseAddress = new Uri(direccionBase!);
    cliente.Timeout = TimeSpan.FromSeconds(3);
});

// Construye la aplicacion web con los servicios registrados.
var aplicacion = constructor.Build();

// Configurar el manejo de errores fuera del entorno de desarrollo.
if (!aplicacion.Environment.IsDevelopment())
{
    aplicacion.UseExceptionHandler("/Inicio/Error");
}

// Establecer la cultura antes de procesar los controladores.
aplicacion.UseRequestLocalization();

// Mostrar los errores HTTP mediante una pagina de la aplicacion.
aplicacion.UseStatusCodePagesWithReExecute("/Inicio/EstadoHttp/{0}");

// Resuelve rutas antes de aplicar autorizacion y controladores.
aplicacion.UseRouting();

aplicacion.UseAuthorization();

aplicacion.MapStaticAssets();

// Pagina inicial mediante la direccion raiz.
aplicacion.MapControllerRoute(
    name: "raiz",
    pattern: "",
    defaults: new
    {
        controller = "Home",
        action = "Index"
    })
    .WithStaticAssets();

// Pagina inicial mediante una direccion en español.
aplicacion.MapControllerRoute(
    name: "inicio",
    pattern: "Inicio",
    defaults: new
    {
        controller = "Home",
        action = "Index"
    })
    .WithStaticAssets();

// Paginas de error de la aplicacion.
aplicacion.MapControllerRoute(
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

// Limitar las rutas de modulos a los cuatro controladores del proyecto.
const string controladoresModulos =
    "^(Clientes|Empleados|Productos|Proveedores)$";

// Listados: /Clientes, /Empleados, /Productos y /Proveedores.
aplicacion.MapControllerRoute(
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

// Operaciones de los modulos mediante nombres en español.
aplicacion.MapControllerRoute(
    name: "operaciones",
    pattern: "{controller}/{action}/{id?}",
    constraints: new
    {
        controller = controladoresModulos,
        action = "^(Detalles|Crear|Buscar|Editar|Eliminar|EliminarConfirmado)$"
    })
    .WithStaticAssets();

aplicacion.Run();

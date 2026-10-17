using Carniceria.Web.Data;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Registrar MVC y validar automáticamente los tokens antifalsificación
// en las solicitudes que pueden modificar datos.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Compartir una instancia del almacén mediante su interfaz.
builder.Services.AddSingleton<IAlmacenCarniceria, AlmacenCarniceria>();

var app = builder.Build();

// Configurar el manejo de errores fuera del entorno de desarrollo.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
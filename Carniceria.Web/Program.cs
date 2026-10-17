using Carniceria.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Registrar los servicios de MVC.
builder.Services.AddControllersWithViews();

// Compartir una instancia del almacén durante la ejecución de la aplicación.
builder.Services.AddSingleton<AlmacenCarniceria>();

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
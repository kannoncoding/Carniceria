using Carniceria.Api.Datos;

var constructor = WebApplication.CreateBuilder(args);

constructor.Services.AddControllers();
constructor.Services.AddSingleton<IAlmacenCarniceria, AlmacenCarniceria>();

var aplicacion = constructor.Build();

aplicacion.UseAuthorization();

aplicacion.MapControllers();

aplicacion.Run();

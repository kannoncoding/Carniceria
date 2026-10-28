var constructor = WebApplication.CreateBuilder(args);

constructor.Services.AddControllers();

var aplicacion = constructor.Build();

aplicacion.UseAuthorization();

aplicacion.MapControllers();

aplicacion.Run();

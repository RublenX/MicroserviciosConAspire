using ClientesApi.Messaging;
using ClientesData.Context;
using ClientesData.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClientesApi
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Añade los servicios comunes de Aspire (service discovery, resiliencia, health checks, OpenTelemetry).
            builder.AddServiceDefaults();

            // Add services to the container.

            builder.Services.AddControllers();

            // Configurar la base de datos (EF Core + provider desde PoCAspire.Data)
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration.GetConnectionString("pocaspiredb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No se encontró una cadena de conexión para PostgreSQL. Proporcione ConnectionStrings:DefaultConnection o ConnectionStrings:pocaspiredb.");
            }

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Inyección de dependencias para repositorios
            builder.Services.AddScoped<IClienteRepository, ClienteRepository>();

            // Configuración de RabbitMQ y publicador de eventos de Cliente
            builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
            builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

            // Swagger / OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Aplica migraciones de EF Core automáticamente en desarrollo (necesario al levantar
            // un contenedor de PostgreSQL nuevo, p.ej. orquestado por Aspire).
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // Muestra interfaz Swagger en desarrollo
                app.UseSwagger();
                app.UseSwaggerUI();
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.MapDefaultEndpoints();

            app.Run();
        }
    }
}

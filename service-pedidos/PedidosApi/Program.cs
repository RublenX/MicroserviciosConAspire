
using Microsoft.EntityFrameworkCore;
using PedidosApplication.Messaging;
using PedidosApplication.Services;
using PedidosData.Context;
using PedidosData.Repositories;

namespace PedidosApi
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
            builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
            builder.Services.AddScoped<IPedidoService, PedidoService>();

            // Configuración de RabbitMQ y consumidor de eventos de Cliente
            builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
            builder.Services.AddHostedService<ClienteEventConsumer>();

            // Swagger / OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Configurar HttpClient para el servicio de Clientes
            builder.Services.AddHttpClient("Clientes", (sp, client) =>
            {
                var url = sp.GetRequiredService<IConfiguration>()["ServiceUrls:Clientes"];
                if (!string.IsNullOrWhiteSpace(url))
                    client.BaseAddress = new Uri(url);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}

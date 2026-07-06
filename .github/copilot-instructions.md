# Instrucciones de contexto para Copilot — MicroserviciosConAspire

> Este archivo se carga automáticamente como contexto en Copilot Chat. Resume el estado
> del laboratorio para evitar tener que re-explorar todo el repositorio en cada hilo nuevo.
> Mantenlo actualizado cuando se añadan servicios, eventos o cambios estructurales relevantes.

## Objetivo del repositorio
Laboratorio de microservicios simples en .NET 10, pensado para integrarse más adelante con
**.NET Aspire** (aún no existe proyecto `orchestrator-aspire` / AppHost). De momento cada
servicio se ejecuta de forma independiente contra contenedores locales (`docker-compose.yml`).

## Estructura (dos soluciones independientes en el mismo repo)
- `MicroservicioClientes.slnx`
  - `service-clientes/ClientesApi` (Microsoft.NET.Sdk.Web) — API REST, controlador `ClienteController` (archivo `Controllers/ClientesController.cs`).
  - `service-clientes/ClientesData` (Microsoft.NET.Sdk) — EF Core + Npgsql, modelo `Cliente` (Id, Nombre, Vip).
- `MicroservicioPedidos.slnx`
  - `service-pedidos/PedidosApi` (Microsoft.NET.Sdk.Web) — API REST, controlador `PedidosController`.
  - `service-pedidos/PedidosApplication` (Microsoft.NET.Sdk, librería) — capa de negocio (`PedidoService`), integraciones externas (HTTP a Clientes, RabbitMQ).
  - `service-pedidos/PedidosData` (Microsoft.NET.Sdk) — EF Core + Npgsql, modelo `Pedido` (Id, IdCliente, Cliente, Fecha, Total).

No hay proyecto de contratos compartido entre ambas soluciones: los DTOs/eventos que cruzan
el límite de servicio se **duplican intencionadamente** en cada lado.

## Stack y versiones relevantes
- `net10.0` en todos los proyectos, `Nullable`/`ImplicitUsings` habilitados.
- EF Core `10.0.9` + `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.2`.
- `RabbitMQ.Client` `7.2.1` (API asíncrona: `IChannel`, `CreateChannelAsync`, `BasicPublishAsync`, `BasicConsumeAsync`, `AsyncEventingBasicConsumer`).
- `Microsoft.Extensions.Hosting.Abstractions` / `Microsoft.Extensions.Options` `10.0.9` en `PedidosApplication` (necesarios porque no es proyecto Web SDK).
- Convención de versionado: paquetes `Microsoft.Extensions.*`/`Microsoft.AspNetCore.*`/EF Core fijados a `10.0.9` (o el patch equivalente); usar `dotnet add package` para resolver versiones reales en vez de adivinarlas.

## Convenciones de código observadas
- Constructores primarios en clases sencillas (`ClienteRepository(AppDbContext db) : IClienteRepository`, `PedidosController(IPedidoService pedidoService)`, etc.).
- Los controladores llaman directamente a repositorios/servicios (sin MediatR/CQRS).
- Dominio y comentarios en español (`Cliente`, `Pedido`, `IdCliente`, comentarios explicativos en español).
- Infraestructura local en `docker-compose.yml`: Postgres compartido (`postgres-common`, usuario/clave `postgres`/`postgres`, puerto 5432) y RabbitMQ (`rabbitmq:management`, usuario/clave `admin`/`admin123`, puertos 5672/15672).
- Cadenas de conexión y configuración sensible al entorno viven en `appsettings.Development.json` (no en `appsettings.json` base).

## Integraciones entre servicios ya implementadas

### 1. HTTP: Pedidos → Clientes (obtención del nombre de cliente)
- `PedidosApplication/Services/PedidoService.cs` usa `IHttpClientFactory` con el cliente nombrado `"Clientes"` (registrado en `PedidosApi/Program.cs`, `BaseAddress` desde `ServiceUrls:Clientes` en `appsettings.Development.json`, apuntando a `https://localhost:7229`).
- Al crear/actualizar un pedido llama a `GET /api/cliente/{id}` y toma el campo `nombre` de la respuesta.

### 2. RabbitMQ: Clientes → Pedidos (eventos de actualización/eliminación de cliente)
- **Publicador** (`service-clientes/ClientesApi/Messaging/`):
  - `RabbitMqOptions` (Host/Port/User/Password, sección de config `RabbitMq`).
  - `ClienteEventContract`: exchange topic `clientes-exchange`, routing keys `cliente.actualizado` y `cliente.eliminado`.
  - `ClienteEventEnvelope` (JSON): `{ EventType, IdCliente, Nombre }`; `ClienteEventTypes.Actualizado = "ClienteActualizado"`, `Eliminado = "ClienteEliminado"`.
  - `IEventPublisher` / `RabbitMqEventPublisher` (singleton, conexión/canal perezosos con `SemaphoreSlim`), inyectado en `ClienteController`, que publica tras `Update`/`Delete` exitosos.
- **Consumidor** (`service-pedidos/PedidosApplication/`):
  - Contrato equivalente en `Events/ClienteEventContract.cs` (mismo exchange/routing keys, más `QueueName = "pedidos.cliente-events-queue"`, binding con patrón `cliente.*`).
  - `Messaging/ClienteEventConsumer.cs`: `BackgroundService` registrado con `AddHostedService<ClienteEventConsumer>()` en `PedidosApi/Program.cs`; declara topología, consume con ack manual (nack + requeue en error), crea un `IServiceScope` por mensaje para resolver `IPedidoService`.
  - Lógica de negocio en `IPedidoService`/`PedidoService`: `ActualizarNombreClienteAsync(idCliente, nombre)` y `EliminarPedidosPorClienteAsync(idCliente)`, apoyadas en `IPedidoRepository.GetByClienteAsync(idCliente)` (nuevo método en `PedidosData`).
  - Config `RabbitMq` (Host/Port/User/Password) también en `appsettings.Development.json` de `PedidosApi`, con las mismas credenciales que `docker-compose.yml`.

## Pendiente / posibles siguientes pasos
- No hay reintentos/resiliencia si RabbitMQ no está disponible al arrancar `PedidosApi` (el `BackgroundService` lanzaría excepción en `StartAsync`).
- Falta el AppHost de .NET Aspire para orquestar ambos servicios + Postgres + RabbitMQ.
- El `README.md` describe el objetivo general del laboratorio pero puede quedar desactualizado respecto a esta sección; usar este archivo como fuente de verdad técnica.

# Instrucciones de contexto para Copilot — MicroserviciosConAspire

> Este archivo se carga automáticamente como contexto en Copilot Chat. Resume el estado
> del laboratorio para evitar tener que re-explorar todo el repositorio en cada hilo nuevo.
> Mantenlo actualizado cuando se añadan servicios, eventos o cambios estructurales relevantes.

## Objetivo del repositorio
Laboratorio de microservicios simples en .NET 10, orquestado localmente con **.NET Aspire**
(`orchestrator-aspire/MicroserviciosConAspire.slnx`). El AppHost levanta los contenedores de
PostgreSQL y RabbitMQ, resuelve mediante service discovery la URL de `ClientesApi` para las
llamadas REST de `PedidosApi`, y ambos servicios se agregan como proyectos existentes para
poder depurarlos. `docker-compose.yml` se mantiene intacto como alternativa para ejecutar los
servicios de forma independiente/standalone (sin Aspire).

## Estructura (tres soluciones independientes en el mismo repo)
- `MicroservicioClientes.slnx`
  - `service-clientes/ClientesApi` (Microsoft.NET.Sdk.Web) — API REST, controlador `ClienteController` (archivo `Controllers/ClientesController.cs`).
  - `service-clientes/ClientesData` (Microsoft.NET.Sdk) — EF Core + Npgsql, modelo `Cliente` (Id, Nombre, Vip).
- `MicroservicioPedidos.slnx`
  - `service-pedidos/PedidosApi` (Microsoft.NET.Sdk.Web) — API REST, controlador `PedidosController`.
  - `service-pedidos/PedidosApplication` (Microsoft.NET.Sdk, librería) — capa de negocio (`PedidoService`), integraciones externas (HTTP a Clientes, RabbitMQ).
  - `service-pedidos/PedidosData` (Microsoft.NET.Sdk) — EF Core + Npgsql, modelo `Pedido` (Id, IdCliente, Cliente, Fecha, Total).
- `orchestrator-aspire/MicroserviciosConAspire.slnx` — solución con carpetas (`/AppHost/`, `/service-clientes/`, `/service-pedidos/`) que agrega:
  - `orchestrator-aspire/MicroserviciosConAspire.AppHost` (`Aspire.AppHost.Sdk`) — orquestador: `AppHost.cs` define Postgres/RabbitMQ y referencia `ClientesApi`/`PedidosApi` como proyectos existentes (`Projects.ClientesApi`, `Projects.PedidosApi`).
  - `orchestrator-aspire/MicroserviciosConAspire.ServiceDefaults` (Microsoft.NET.Sdk) — generado con la plantilla `aspire-servicedefaults`; `Extensions.cs` expone `AddServiceDefaults()`/`MapDefaultEndpoints()` (service discovery, resiliencia HTTP, health checks, OpenTelemetry). Referenciado por `ClientesApi` y `PedidosApi`.
  - Los 5 proyectos de `service-clientes`/`service-pedidos` se agregan como proyectos existentes (mismos `Id` que en sus soluciones originales).

No hay proyecto de contratos compartido entre las soluciones: los DTOs/eventos que cruzan
el límite de servicio se **duplican intencionadamente** en cada lado.

## Stack y versiones relevantes
- `net10.0` en todos los proyectos, `Nullable`/`ImplicitUsings` habilitados.
- EF Core `10.0.9` + `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.2`.
- `RabbitMQ.Client` `7.2.1` (API asíncrona: `IChannel`, `CreateChannelAsync`, `BasicPublishAsync`, `BasicConsumeAsync`, `AsyncEventingBasicConsumer`).
- `Microsoft.Extensions.Hosting.Abstractions` / `Microsoft.Extensions.Options` `10.0.9` en `PedidosApplication` (necesarios porque no es proyecto Web SDK).
- `.NET Aspire` `13.4.6` (plantillas `Aspire.ProjectTemplates`, paquetes `Aspire.Hosting.AppHost`/`Aspire.Hosting.PostgreSQL`/`Aspire.Hosting.RabbitMQ` en el AppHost). `MicroserviciosConAspire.ServiceDefaults` usa `Microsoft.Extensions.ServiceDiscovery`/`Microsoft.Extensions.Http.Resilience` `10.6.0` y `OpenTelemetry.*` `1.15.x`.
- Convención de versionado: paquetes `Microsoft.Extensions.*`/`Microsoft.AspNetCore.*`/EF Core fijados a `10.0.9` (o el patch equivalente); usar `dotnet add package` para resolver versiones reales en vez de adivinarlas.

## Convenciones de código observadas
- Constructores primarios en clases sencillas (`ClienteRepository(AppDbContext db) : IClienteRepository`, `PedidosController(IPedidoService pedidoService)`, etc.).
- Los controladores llaman directamente a repositorios/servicios (sin MediatR/CQRS).
- Dominio y comentarios en español (`Cliente`, `Pedido`, `IdCliente`, comentarios explicativos en español).
- Infraestructura local en `docker-compose.yml`: Postgres compartido (`postgres-common`, usuario/clave `postgres`/`postgres`, puerto 5432) y RabbitMQ (`rabbitmq:management`, usuario/clave `admin`/`admin123`, puertos 5672/15672).
- Cadenas de conexión y configuración sensible al entorno viven en `appsettings.Development.json` (no en `appsettings.json` base).

## Integraciones entre servicios ya implementadas

### 1. HTTP: Pedidos → Clientes (obtención del nombre de cliente)
- `PedidosApplication/Services/PedidoService.cs` usa `IHttpClientFactory` con el cliente nombrado `"Clientes"` (registrado en `PedidosApi/Program.cs`). `BaseAddress = "https+http://clientesapi"`, resuelto por **service discovery de Aspire** (`AddServiceDefaults()` + `WithReference(clientesApi)` en el AppHost) cuando se ejecuta vía `MicroserviciosConAspire.AppHost`.
- Fallback para ejecución standalone (fuera de Aspire, contra `docker-compose.yml`): sección `Services:clientesapi:https/http` en `PedidosApi/appsettings.Development.json` → `https://localhost:7229` / `http://localhost:5149` (mismo mecanismo de `Microsoft.Extensions.ServiceDiscovery`, configurado de forma estática).
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

## Orquestación local con .NET Aspire (`orchestrator-aspire/`)
- `AppHost.cs` (top-level statements, sin `Program.cs`): define `postgres` (`AddPostgres` con parámetros `postgres-user`/`postgres-password` = `postgres`/`postgres`, `WithDataVolume()`) con dos bases `clientesdb`/`pedidosdb` (`AddDatabase`, nombres reales `servicioclientesdb`/`serviciopedidosdb`); y `rabbitmq` (`AddRabbitMQ` con parámetros `rabbitmq-user`/`rabbitmq-password` = `admin`/`admin123`, `WithManagementPlugin()`, `WithDataVolume()`) — mismas credenciales que `docker-compose.yml`.
- **Conexión a bases de datos sin tocar código**: cada base se inyecta con `WithReference(db, connectionName: "DefaultConnection")`, así `ClientesApi`/`PedidosApi` siguen leyendo `ConnectionStrings:DefaultConnection` tal cual (sin cambios en `Program.cs` para EF Core).
- **RabbitMQ sin tocar `RabbitMqOptions`**: en vez del componente cliente `Aspire.RabbitMQ.Client`, se inyectan variables de entorno discretas `RabbitMq__Host`/`RabbitMq__Port`/`RabbitMq__User`/`RabbitMq__Password` (desde `rabbitmq.GetEndpoint("tcp").Property(EndpointProperty.Host/Port)` y los parámetros de usuario/contraseña) a ambos proyectos, para que `RabbitMqEventPublisher`/`ClienteEventConsumer` sigan funcionando sin cambios.
- **Service discovery**: `pedidosapi` se registra con `WithReference(clientesApi)`; combinado con `AddServiceDefaults()` (proyecto `MicroserviciosConAspire.ServiceDefaults`, plantilla `aspire-servicedefaults`) esto permite que `https+http://clientesapi` se resuelva automáticamente a la URL real de `ClientesApi`.
- **Orden de arranque**: `WaitFor(...)` se usa en ambos proyectos para esperar a que Postgres/RabbitMQ (y `ClientesApi`, en el caso de `PedidosApi`) estén listos antes de arrancar.
- **Migraciones EF Core automáticas**: como Aspire crea contenedores Postgres nuevos, ambos `Program.cs` aplican `db.Database.Migrate()` en un scope al arrancar (sólo en `IsDevelopment()`), usando las migraciones `InitialCreate` ya existentes en `ClientesData`/`PedidosData`.
- `ClientesApi`/`PedidosApi` añaden `builder.AddServiceDefaults()` tras crear el builder y `app.MapDefaultEndpoints()` antes de `app.Run()` (expone `/health` y `/alive` en desarrollo).

## Pendiente / posibles siguientes pasos
- No hay reintentos/resiliencia ante caídas de RabbitMQ **después** del arranque (el `BackgroundService`/publisher no reconectarían solos si se pierde la conexión ya establecida); `WaitFor` sólo cubre el arranque inicial.
- No se ha migrado la mensajería a `Aspire.RabbitMQ.Client` (se optó por variables de entorno discretas para no reescribir `RabbitMqEventPublisher`/`ClienteEventConsumer`); sería el siguiente paso si se quiere health-check/telemetría nativa de RabbitMQ.
- No se ha añadido `WithPgAdmin()` ni despliegue a Azure (`azd`/Azure Container Apps) para el AppHost.
- El `README.md` describe el objetivo general del laboratorio pero puede quedar desactualizado respecto a esta sección; usar este archivo como fuente de verdad técnica.

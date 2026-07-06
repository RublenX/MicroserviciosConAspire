# Microservicios con Aspire

Este repositorio funciona como un laboratorio práctico para probar y desarrollar microservicios en un entorno basado en .NET Aspire. La idea es ir añadiendo nuevos servicios, dependencias y escenarios de orquestación para validar cómo se comporta una arquitectura distribuida de forma local antes de pasar a entornos más complejos.

## Qué incluye este proyecto

En este momento la solución contiene un ejemplo básico de microservicio orientado a operaciones de pedidos/clientes:

- `service-clientes/ClientesApi`: API REST en ASP.NET Core.
- `service-clientes/ClientesData`: capa de acceso a datos con EF Core y PostgreSQL.
- `service-pedidos` y `orchestrator-aspire`: carpetas previstas para ampliar el laboratorio con más servicios y un host de Aspire.

## Requisitos previos

- .NET SDK 10
- PostgreSQL en ejecución
- Un cliente HTTP como `curl`, Postman o Swagger UI

## Configuración

El servicio de API espera una cadena de conexión a PostgreSQL. Puedes proporcionarla mediante una variable de entorno o mediante `appsettings.Development.json`:

- `ConnectionStrings__DefaultConnection`
- o `ConnectionStrings__pocaspiredb`

Ejemplo con variable de entorno en PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Database=pocaspiredb;Username=postgres;Password=postgres"
```

## Compilar y ejecutar

Desde la raíz del repositorio:

```powershell
dotnet build .\MicroservicioClientes.slnx
dotnet run --project .\service-clientes\ClientesApi\ClientesApi.csproj
```

Por defecto, la API queda disponible en:

- `http://localhost:5149`
- `https://localhost:7229`

## Migraciones de EF Core
Requisitos previos:
- Tener instalado el SDK de .NET 10
- Instalar el paquete _Microsoft.EntityFrameworkCore.Design_ en el proyecto inicial
- Instalar el CLI de EF Core si no lo tienes:
```powershell
dotnet tool install --global dotnet-ef
```
- Inicializar el proyecto de EF Core en la carpeta `service-clientes/ClientesData`:
```powershell
dotnet ef migrations add InitialCreate --project .\service-Xxx\XxxData\XxxData.csproj --startup-project .\service-Xxx\XxxApi\XxxApi.csproj
```
- Crear la base de datos y aplicar las migraciones:
```powershell
dotnet ef database update --project .\service-Xxx\XxxData\XxxData.csproj --startup-project .\service-Xxx\XxxApi\XxxApi.csproj
```

## Probar la API

La API expone endpoints bajo el prefijo `api/pedido` y se puede probar directamente desde Swagger cuando el entorno es Development.

Ruta de Swagger:

- `http://localhost:5149/swagger`

Ejemplo de petición:

```powershell
curl http://localhost:5149/api/pedido
```

## Estructura de la solución

- `service-clientes/ClientesApi`: punto de entrada HTTP del microservicio.
- `service-clientes/ClientesData`: modelo y acceso a datos.

## Objetivo del laboratorio

Este proyecto está pensado para servir como base de pruebas para:

- experimentar con microservicios simples y su comunicación
- validar el uso de Aspire como orquestador local
- probar patrones de acceso a datos con EF Core
- iterar sobre APIs REST con Swagger y pruebas manuales

## Contribuir

Si quieres ampliar este laboratorio:

1. añade nuevos proyectos de servicio bajo carpetas separadas
2. integra los servicios en un host de Aspire
3. documenta los cambios y los escenarios probados
4. mantén los ejemplos simples y reproducibles

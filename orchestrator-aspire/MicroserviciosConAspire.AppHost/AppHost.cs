var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL: mismo usuario/contraseña que docker-compose.yml, con volumen de datos persistente.
var postgresUser = builder.AddParameter("postgres-user", "postgres");
var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithDataVolume();

var clientesDb = postgres.AddDatabase("clientesdb", databaseName: "servicioclientesdb");
var pedidosDb = postgres.AddDatabase("pedidosdb", databaseName: "serviciopedidosdb");

// RabbitMQ: mismo usuario/contraseña que docker-compose.yml, con plugin de administración y volumen persistente.
var rabbitMqUser = builder.AddParameter("rabbitmq-user", "admin");
var rabbitMqPassword = builder.AddParameter("rabbitmq-password", "admin123", secret: true);

var rabbitmq = builder.AddRabbitMQ("rabbitmq", rabbitMqUser, rabbitMqPassword)
    .WithManagementPlugin()
    .WithDataVolume();

var rabbitmqEndpoint = rabbitmq.GetEndpoint("tcp");

// ClientesApi: base de datos propia (inyectada como "DefaultConnection") y RabbitMq (host/puerto/credenciales
// inyectados como variables discretas para no tener que tocar RabbitMqOptions/RabbitMqEventPublisher).
var clientesApi = builder.AddProject<Projects.ClientesApi>("clientesapi")
    .WithReference(clientesDb, connectionName: "DefaultConnection")
    .WaitFor(clientesDb)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("RabbitMq__Host", rabbitmqEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RabbitMq__Port", rabbitmqEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("RabbitMq__User", rabbitMqUser)
    .WithEnvironment("RabbitMq__Password", rabbitMqPassword);

// PedidosApi: su propia base de datos, RabbitMq, y referencia a ClientesApi para que el HttpClient
// "Clientes" (https+http://clientesapi) se resuelva mediante service discovery de Aspire.
var pedidosApi = builder.AddProject<Projects.PedidosApi>("pedidosapi")
    .WithReference(pedidosDb, connectionName: "DefaultConnection")
    .WaitFor(pedidosDb)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("RabbitMq__Host", rabbitmqEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RabbitMq__Port", rabbitmqEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("RabbitMq__User", rabbitMqUser)
    .WithEnvironment("RabbitMq__Password", rabbitMqPassword)
    .WithReference(clientesApi)
    .WaitFor(clientesApi);

builder.Build().Run();

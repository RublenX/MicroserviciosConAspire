using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ClientesApi.Messaging
{
    // Publica los eventos de Cliente en RabbitMQ. La conexión y el canal se crean de forma
    // perezosa y se reutilizan durante toda la vida de la aplicación (registrado como singleton).
    public sealed class RabbitMqEventPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventPublisher> logger) : IEventPublisher, IAsyncDisposable
    {
        private readonly RabbitMqOptions _options = options.Value;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private IConnection? _connection;
        private IChannel? _channel;

        public Task PublicarClienteActualizadoAsync(int id, string nombre, CancellationToken cancellationToken = default)
        {
            var evento = new ClienteEventEnvelope
            {
                EventType = ClienteEventTypes.Actualizado,
                IdCliente = id,
                Nombre = nombre
            };

            return PublicarAsync(ClienteEventContract.RoutingKeyActualizado, evento, cancellationToken);
        }

        public Task PublicarClienteEliminadoAsync(int id, CancellationToken cancellationToken = default)
        {
            var evento = new ClienteEventEnvelope
            {
                EventType = ClienteEventTypes.Eliminado,
                IdCliente = id
            };

            return PublicarAsync(ClienteEventContract.RoutingKeyEliminado, evento, cancellationToken);
        }

        private async Task PublicarAsync(string routingKey, ClienteEventEnvelope evento, CancellationToken cancellationToken)
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(evento);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: ClienteEventContract.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogInformation("Evento '{EventType}' publicado para el cliente {IdCliente}", evento.EventType, evento.IdCliente);
        }

        private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
        {
            if (_channel is { IsOpen: true })
                return _channel;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_channel is { IsOpen: true })
                    return _channel;

                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    UserName = _options.User,
                    Password = _options.Password
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await _channel.ExchangeDeclareAsync(
                    exchange: ClienteEventContract.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    cancellationToken: cancellationToken);

                return _channel;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null)
                await _channel.CloseAsync();

            if (_connection is not null)
                await _connection.CloseAsync();

            _initLock.Dispose();
        }
    }
}

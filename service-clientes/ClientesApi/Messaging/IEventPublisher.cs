namespace ClientesApi.Messaging
{
    public interface IEventPublisher
    {
        Task PublicarClienteActualizadoAsync(int id, string nombre, CancellationToken cancellationToken = default);
        Task PublicarClienteEliminadoAsync(int id, CancellationToken cancellationToken = default);
    }
}

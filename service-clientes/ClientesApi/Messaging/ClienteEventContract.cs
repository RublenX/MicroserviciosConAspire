namespace ClientesApi.Messaging
{
    // Nombre del exchange y routing keys usados para publicar los eventos de Cliente.
    // Deben coincidir exactamente con los declarados en el consumidor del microservicio de Pedidos.
    public static class ClienteEventContract
    {
        public const string ExchangeName = "clientes-exchange";
        public const string RoutingKeyActualizado = "cliente.actualizado";
        public const string RoutingKeyEliminado = "cliente.eliminado";
    }

    // Tipos de evento posibles dentro del envoltorio ClienteEventEnvelope.
    public static class ClienteEventTypes
    {
        public const string Actualizado = "ClienteActualizado";
        public const string Eliminado = "ClienteEliminado";
    }

    // Envoltorio serializado como JSON en el cuerpo del mensaje de RabbitMQ.
    public class ClienteEventEnvelope
    {
        public string EventType { get; set; } = string.Empty;
        public int IdCliente { get; set; }
        public string? Nombre { get; set; }
    }
}

namespace ClientesData.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        public required string Nombre { get; set; }
        public bool Vip { get; set; } = false;
    }
}

using ClientesData.Models;

namespace ClientesData.Repositories
{
    public interface IClienteRepository
    {
        Task<IEnumerable<Cliente>> GetAllAsync();
        Task<Cliente?> GetAsync(int id);
        Task AddAsync(Cliente pedido);
        Task UpdateAsync(Cliente pedido);
        Task DeleteAsync(int id);
    }
}

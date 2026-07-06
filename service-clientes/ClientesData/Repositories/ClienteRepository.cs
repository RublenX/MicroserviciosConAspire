using Microsoft.EntityFrameworkCore;
using ClientesData.Context;
using ClientesData.Models;

namespace ClientesData.Repositories
{
    public class ClienteRepository(AppDbContext db) : IClienteRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<IEnumerable<Cliente>> GetAllAsync() => await _db.Cliente.ToListAsync();

        public async Task<Cliente?> GetAsync(int id) => await _db.Cliente.FindAsync(id);

        public async Task AddAsync(Cliente cliente)
        {
            _db.Cliente.Add(cliente);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Cliente pedido)
        {
            _db.Entry(pedido).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var p = await _db.Cliente.FindAsync(id);
            if (p == null) return;
            _db.Cliente.Remove(p);
            await _db.SaveChangesAsync();
        }
    }
}

using ClientesData.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientesData.Context
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Cliente> Cliente => Set<Cliente>();
    }
}

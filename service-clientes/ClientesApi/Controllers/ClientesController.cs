using ClientesApi.Messaging;
using ClientesData.Models;
using ClientesData.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientesApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClienteController(IClienteRepository clienteRepository, IEventPublisher eventPublisher) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetAll()
        {
            var clientes = await clienteRepository.GetAllAsync();
            return Ok(clientes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> Get(int id)
        {
            var cliente = await clienteRepository.GetAsync(id);
            if (cliente == null) return NotFound();
            return Ok(cliente);
        }

        [HttpPost]
        public async Task<ActionResult<Cliente>> Create(Cliente cliente)
        {
            await clienteRepository.AddAsync(cliente);
            return CreatedAtAction(nameof(Get), new { id = cliente.Id }, cliente);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Cliente cliente)
        {
            if (id != cliente.Id) return BadRequest();
            try
            {
                await clienteRepository.UpdateAsync(cliente);
            }
            catch (DbUpdateConcurrencyException)
            {
                var clienteGet = await clienteRepository.GetAsync(id);
                if (clienteGet == null) return NotFound();
                throw;
            }

            // Notifica al bus de mensajería que el cliente ha sido actualizado
            await eventPublisher.PublicarClienteActualizadoAsync(cliente.Id, cliente.Nombre);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cliente = await clienteRepository.GetAsync(id);
            if (cliente == null) return NotFound();
            await clienteRepository.DeleteAsync(cliente.Id);

            // Notifica al bus de mensajería que el cliente ha sido eliminado
            await eventPublisher.PublicarClienteEliminadoAsync(cliente.Id);

            return NoContent();
        }
    }
}

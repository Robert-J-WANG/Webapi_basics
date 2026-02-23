using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public interface IOrderRepository
{
    Task<List<Order>> GetAllAsync(CancellationToken cancellationToken);
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken);
    Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken);
}
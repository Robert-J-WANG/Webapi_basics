using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public interface IOrderRepository
{
    Task<List<Order>> GetAllAsync();
    Task<Order?> GetByIdAsync(int id);
    Task<Order> AddAsync(decimal amount);
    Task UpdateStatusAsync(int id, string status);
}
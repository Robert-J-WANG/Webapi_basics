using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(IOrderRepository repo)
{
    public async Task<List<Order>> GetAllAsync() => await repo.GetAllAsync();

    public async Task<Order> GetByIdAsync(int id) => await repo.GetByIdAsync(id) ?? throw new OrderNotFoundException(id);

    public async Task<Order> CreateAsync(OrderCreateRequest request) => await repo.AddAsync(request.Amount);

    public async Task<Order> PayAsync(int id)
    {
        var order = await repo.GetByIdAsync(id);
        if (order is null)
            throw new OrderNotFoundException(id);
        if (order.Status == "Paid")
            throw new OrderConflictException($"Order {id} was already paid");
        await repo.UpdateStatusAsync(id, "Paid");
        return order;
    }
}
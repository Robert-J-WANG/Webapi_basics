using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(IOrderRepository repo)
{
    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken) =>
        await repo.GetAllAsync(cancellationToken);

    public async Task<Order> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await repo.GetByIdAsync(id, cancellationToken) ?? throw new OrderNotFoundException(id);

    public async Task<Order> CreateAsync(OrderCreateRequest request, CancellationToken cancellationToken) =>
        await repo.AddAsync(request.Amount, cancellationToken);

    public async Task<Order> PayAsync(int id, CancellationToken cancellationToken)
    {
        var order = await repo.GetByIdAsync(id, cancellationToken);
        if (order is null)
            throw new OrderNotFoundException(id);
        if (order.Status == "Paid")
            throw new OrderConflictException($"Order {id} was already paid");
        await repo.UpdateStatusAsync(id, "Paid", cancellationToken);
        return order;
    }
}
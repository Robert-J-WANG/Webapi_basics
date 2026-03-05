using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(IOrderRepository repo, ILogger<OrderService> logger)
{
    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken)=>await repo.GetAllAsync(query,cancellationToken);

    public async Task<Order> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await repo.GetByIdAsync(id, cancellationToken) ?? throw new OrderNotFoundException(id);

    public async Task<Order> CreateAsync(OrderCreateRequest request, CancellationToken cancellationToken) =>
        await repo.AddAsync(request.Amount, cancellationToken);

    public async Task<Order> PayAsync(int id, CancellationToken cancellationToken)
    {
        // 使用BeginScope， 作用域直到本函数 return
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["OrderId"] = id
        });

        
        
        logger.LogInformation("Pay started.");
        var order = await repo.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Pay failed: order not found.");
            throw new OrderNotFoundException(id);
        }
        if (order.Status == "Paid")
        {
            logger.LogWarning("Pay failed: order already paid.");
            throw new OrderConflictException($"Order {id} was already paid");
        }
            
        await repo.UpdateStatusAsync(id, "Paid", cancellationToken);
        logger.LogInformation("Pay succeeded");
        return order;
    }
    
}
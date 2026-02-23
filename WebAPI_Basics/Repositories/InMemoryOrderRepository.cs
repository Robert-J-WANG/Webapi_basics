using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public class InMemoryOrderRepository : IOrderRepository
{
    private static readonly List<Order> Orders =
    [
        new Order
        {
            Id = 1,
            Amount = 100m,
            Status = "Created"
        },
        new Order
        {
            Id = 2,
            Amount = 200m,
            Status = "Paid"
        }
    ];

    public Task<List<Order>> GetAllAsync() => Task.FromResult(Orders.ToList());

    public Task<Order?> GetByIdAsync(int id) => Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));

    public Task<Order> AddAsync(decimal amount)
    {
        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new Order()
        {
            Id = nextId,
            Amount = amount,
            Status = "Created"
        };
        Orders.Add(order);
        return Task.FromResult(order);
    }

    public Task UpdateStatusAsync(int id, string status)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order != null)
        {
            order.Status = status;
        }
        return Task.CompletedTask;
    }
}
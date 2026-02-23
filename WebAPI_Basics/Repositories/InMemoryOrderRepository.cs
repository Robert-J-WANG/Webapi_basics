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

    public List<Order> GetAll() => Orders.ToList();

    public Order? GetById(int id) => Orders.FirstOrDefault(x => x.Id == id);

    public Order Add(decimal amount)
    {
        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new Order()
        {
            Id = nextId,
            Amount = amount,
            Status = "Created"
        };
        Orders.Add(order);
        return order;
    }

    public void UpdateStatus(int id, string status)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order is null)
            return;
        order.Status = status;
    }
}
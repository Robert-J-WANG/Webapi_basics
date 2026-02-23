using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;

namespace WebAPI_Basics.Services;

public class OrderService
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

    public List<Order> GetAll()
    {
        return Orders.ToList();
    }

    public Order GetById(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        if (order is null)
            throw new OrderNotFoundException(id);
        return order;
    }

    public Order Create(OrderCreateRequest request)
    {
        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new Order()
        {
            Id = nextId,
            Amount = request.Amount,
            Status = "Created"
        };
        Orders.Add(order);
        return order;
    }

    public Order Pay(int id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order is null)
            throw new OrderNotFoundException(id);
        if (order.Status == "Paid")
            throw new OrderConflictException($"Order {id} was already paid");
        order.Status = "Paid";
        return order;
    }
}
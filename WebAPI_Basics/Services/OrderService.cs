using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;

namespace WebAPI_Basics.Services;

public class OrderService
{
    public class OrderItem
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private static readonly List<OrderItem> Orders =
    [
        new OrderItem
        {
            Id = 1,
            Amount = 100m,
            Status = "Created"
        },
        new OrderItem
        {
            Id = 2,
            Amount = 200m,
            Status = "Paid"
        }
    ];

    public List<OrderItem> GetAll()
    {
        return Orders.ToList();
    }

    public OrderItem GetById(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        if (order is null)
            throw new OrderNotFoundException(id);
        return order;
    }

    public OrderItem Create(OrderCreateRequest request)
    {
        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new OrderItem()
        {
            Id = nextId,
            Amount = request.Amount,
            Status = "Created"
        };
        Orders.Add(order);
        return order;
    }

    public OrderItem Pay(int id)
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
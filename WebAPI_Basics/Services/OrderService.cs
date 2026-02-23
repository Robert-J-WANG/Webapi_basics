using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(IOrderRepository repo)
{
    public List<Order> GetAll() => repo.GetAll();

    public Order GetById(int id) => repo.GetById(id) ?? throw new OrderNotFoundException(id);

    public Order Create(OrderCreateRequest request) => repo.Add(request.Amount);

    public Order Pay(int id)
    {
        var order = repo.GetById(id);
        if (order is null)
            throw new OrderNotFoundException(id);
        if (order.Status == "Paid")
            throw new OrderConflictException($"Order {id} was already paid");
        repo.UpdateStatus(id, "Paid");
        return order;
    }
}
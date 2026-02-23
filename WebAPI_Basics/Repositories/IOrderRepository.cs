using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public interface IOrderRepository
{
    List<Order> GetAll();
    Order? GetById(int id);
    Order Add(decimal amount);
    void UpdateStatus(int id, string status);
}
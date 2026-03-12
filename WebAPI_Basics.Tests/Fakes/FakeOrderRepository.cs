
using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Tests.Fakes;

public class FakeOrderRepository:IOrderRepository
{
    // 用 Dictionary 模拟“数据库表”：key=id，value=Order
    private readonly Dictionary<int, Order> _store = new();
	

    // 记录 UpdateStatusAsync 被调用次数，用于断言 Service 有没有真的“更新”
    public int UpdateStatusCallCount { get; private set; }

    // Seed = 测试准备数据：提前放一条订单到“内存表”里
    public void Seed(Order order) => _store[order.Id] = order;

    public Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken ct)
        => Task.FromResult(_store.Values.ToList());

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct)
        => Task.FromResult(_store.TryGetValue(id, out var o) ? o : null);

    public Task<Order> AddAsync(decimal amount, CancellationToken ct)
    {
        var id = _store.Count == 0 ? 1 : _store.Keys.Max() + 1;
        var order = new Order()
        {
            Id = id,
            Amount = amount,
            Status = "Created",
        };

        _store[id] = order;
        return Task.FromResult(order);
    }

    public Task UpdateStatusAsync(int id, string status, CancellationToken ct)
    {
        UpdateStatusCallCount++;

        if (_store.TryGetValue(id, out var o))
        {
            o.Status = status;
        }

        return Task.CompletedTask;
    }
}
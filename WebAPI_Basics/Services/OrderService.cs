using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(IOrderRepository repo)
{
    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken)
    {
        var orders = await repo.GetAllAsync(cancellationToken);
        // 根据query参数，对数据集处理
        // 1) 过滤（status）
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            orders = orders.Where(o => string.Equals(o.Status, query.Status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // 2) 排序（sortBy + sortDir）
        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var sortDir = query.SortDir?.Trim().ToLowerInvariant();

        var desc = sortDir == "desc";

        // switch语句新语法
        orders = sortBy switch
        {
            "amount" => desc
                ? orders.OrderByDescending(x => x.Amount).ToList()
                : orders.OrderBy(x => x.Amount).ToList(),

            "id" => desc
                ? orders.OrderByDescending(x => x.Id).ToList()
                : orders.OrderBy(x => x.Id).ToList(),

            _ => orders.OrderBy(x => x.Id).ToList() // 默认按 Id 升序
        };

        // 还原后的老式写法（不再是表达式，而是语句块）
        /*
        switch (sortBy)
        {
            case "amount":
                if (desc)
                {
                    orders = orders.OrderByDescending(x => x.Amount).ToList();
                }
                else
                {
                    orders = orders.OrderBy(x => x.Amount).ToList();
                }
                break;

            case "id":
                if (desc)
                {
                    orders = orders.OrderByDescending(x => x.Id).ToList();
                }
                else
                {
                    orders = orders.OrderBy(x => x.Id).ToList();
                }
                break;

            default:
                orders = orders.OrderBy(x => x.Id).ToList(); // 默认按 Id 升序
                break;
        }
        */

        // 3) 分页（page + pageSize）
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = Math.Min(pageSize, 100); // 基础保护，避免一次拿太多

        orders = orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return orders;
    }


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
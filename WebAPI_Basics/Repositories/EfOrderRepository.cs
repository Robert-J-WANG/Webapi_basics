using Microsoft.EntityFrameworkCore;
using WebAPI_Basics.Data;
using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;

namespace WebAPI_Basics.Repositories;

public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 拿到数据表数据集合对象
        DbSet<Order> orders = db.Orders;

        // 执行查询过滤的操作
        // 定义一个变量来保存链式查询的结果
        IQueryable<Order> result = orders; // 初始化一下，防止为空
        // 1) 过滤
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            result = result.Where(o => o.Status == status); 
        }

        // 2) 排序
        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var sortDir = query.SortDir?.Trim().ToLowerInvariant();
        var desc = sortDir == "desc";

        result = sortBy switch
        {
            "amount" => desc ? result.OrderByDescending(x => x.Amount) : result.OrderBy(x => x.Amount),
            "id"     => desc ? result.OrderByDescending(x => x.Id)     : result.OrderBy(x => x.Id),
            _        => result.OrderBy(x => x.Id)
        };

        // 3) 分页
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = Math.Min(pageSize, 100);

        result = result.Skip((page - 1) * pageSize).Take(pageSize);

        return await result.ToListAsync(cancellationToken);

    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await db.Set<Order>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // EF + SQL Server 会自动生成 Id（Identity）
        var order = new Order
        {
            Amount = amount,
            Status = "Created"
        };

        await db.Set<Order>().AddAsync(order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        // SaveChanges 后 order.Id 会被填上
        return order;
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = await db.Set<Order>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return;

        order.Status = status;
        await db.SaveChangesAsync(cancellationToken);
    }
}
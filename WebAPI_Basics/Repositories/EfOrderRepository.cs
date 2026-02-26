using Microsoft.EntityFrameworkCore;
using WebAPI_Basics.Data;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await db.Set<Order>()
            .ToListAsync(cancellationToken);
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
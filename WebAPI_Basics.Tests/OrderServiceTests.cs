using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebAPI_Basics.Domain;
using WebAPI_Basics.Services;
using WebAPI_Basics.Tests.Fakes;

namespace WebAPI_Basics.Tests;

public class OrderServiceTests
{
    [Fact]
    public async Task PayAsync_WhenCreatedOrder_ShouldUpdateToPaid()
    {
        // Arrange：准备一个 Created 订单
        var repo = new FakeOrderRepository();
        repo.Seed(new Order
        {
            Id = 1,
            Amount = 10,
            Status = "Created",
        });

        var logger = NullLogger<OrderService>.Instance;
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var service = new OrderService(repo, logger);

        // Act：执行 Pay
        var order = await service.PayAsync(1, CancellationToken.None);

        // Assert：业务期望——状态变 Paid；更新被调用一次
        Assert.Equal("Paid", order.Status);
        Assert.Equal(1, repo.UpdateStatusCallCount);
    }
    
    [Fact]
    public async Task PayAsync_WhenOrderNotFound_ShouldThrowNotFound()
    {
        // Arrange：不 seed 数据 => 查不到订单
        var repo = new FakeOrderRepository();

        var logger = NullLogger<OrderService>.Instance;
        // var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var service = new OrderService(repo, logger);

        // Act + Assert：必须抛 NotFound 异常
        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => service.PayAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task PayAsync_WhenAlreadyPaid_ShouldThrowConflict()
    {
        // Arrange：seed 一条 Paid 订单
        var repo = new FakeOrderRepository();
        repo.Seed(new Order
        {
            Id = 2,
            Amount = 10,
            Status = "Paid"
        });
        

        var logger = NullLogger<OrderService>.Instance;
        // var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var service = new OrderService(repo, logger);

        // Act + Assert：必须抛 Conflict 异常
        await Assert.ThrowsAsync<OrderConflictException>(
            () => service.PayAsync(2, CancellationToken.None));

        // Assert：冲突时不应更新
        Assert.Equal(0, repo.UpdateStatusCallCount);
    }
}
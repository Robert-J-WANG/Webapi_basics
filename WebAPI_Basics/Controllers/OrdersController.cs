using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Services;

namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController(OrderService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderResponse>>> GetAll([FromQuery] OrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        var result = (await service.GetAllAsync(query, cancellationToken)).Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var order = await service.GetByIdAsync(id, cancellationToken);
        return Ok(ToResponse(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var order = await service.CreateAsync(request, cancellationToken);
        var response = ToResponse(order);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response
        );
    }

    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<OrderResponse>> Pay(int id, CancellationToken cancellationToken)
    {
        var order = await service.PayAsync(id, cancellationToken);
        return Ok(ToResponse(order));
    }


    //辅助方法： 把OrderItem转换成OrderResponse
    private OrderResponse ToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            Amount = order.Amount,
            Status = order.Status,
        };
    }
}
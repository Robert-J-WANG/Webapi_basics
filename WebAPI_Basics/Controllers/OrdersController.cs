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
    public async Task<ActionResult<List<OrderResponse>>> GetAll()
    {
        var result = (await service.GetAllAsync()).Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        try
        {
            var order = await service.GetByIdAsync(id);
            return Ok(ToResponse(order));
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request)
    {
        var order = await service.CreateAsync(request);
        var response = ToResponse(order);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response
        );
    }

    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<OrderResponse>> Pay(int id)
    {
        try
        {
            var order = await service.PayAsync(id);
            return Ok(ToResponse(order));
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
        catch (OrderConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
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
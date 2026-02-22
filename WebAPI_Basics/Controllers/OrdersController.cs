using Microsoft.AspNetCore.Mvc;
using WebAPI_Basics.Domain;
using WebAPI_Basics.Dtos.Requests;

namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    private class OrderItem
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Created";
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

    [HttpPost("{id:int}/pay")]
    public ActionResult<OrderResponse> Pay(int id)
    {
        try
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order == null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException("Order is already paid");
            order.Status = "Paid";
            
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

    [HttpGet]
    public ActionResult<OrderResponse> GetAll()
    {
        var result = Orders.Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public ActionResult<OrderResponse> GetById(int id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(ToResponse(order));
    }

    [HttpPost]
    public ActionResult<OrderResponse> Create(OrderCreateRequest request)
    {
        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new OrderItem
        {
            Id = nextId,
            Amount = request.Amount,
            Status = "Created"
        };

        Orders.Add(order);
        var response = ToResponse(order);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response
        );
    }

    //辅助方法： 把OrderItem转换成OrderResponse
    private OrderResponse ToResponse(OrderItem order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            Amount = order.Amount,
            Status = order.Status,
        };
    }
}
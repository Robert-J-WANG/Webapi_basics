using Microsoft.AspNetCore.Mvc;
using WebAPI_Basics.Dtos.Requests;

namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    private record OrderItem(int Id, decimal Amount, string Status);

    private static readonly List<OrderItem> Orders =
    [
        new(1, 100m, "Created"),
        new(2, 200m, "Paid")
    ];

    [HttpGet]
    public ActionResult GetAll()
    {
        var result = Orders.Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public ActionResult GetById(int id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(ToResponse(order));
    }

    [HttpPost]
    public ActionResult Create(OrderCreateRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than 0." });
        }

        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new OrderItem(nextId, request.Amount, "Created");

        Orders.Add(order);

        // return Ok(ToResponse(order));

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
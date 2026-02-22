using Microsoft.AspNetCore.Mvc;

namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController:ControllerBase
{
    private record OrderItem(int Id, decimal Amount, string Status);

    private static readonly List<OrderItem> Orders =
    [
        new(1, 100m, "Created"),
        new(2, 200m, "Paid")
    ];
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(Orders);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var order=Orders.FirstOrDefault(x=>x.Id==id);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }
}
namespace WebAPI_Basics.Dtos.Requests;

public class OrderUpdateQueryRequest
{
    public decimal Amount { get; set; }
    public string Status { get; set; }="Created";
}
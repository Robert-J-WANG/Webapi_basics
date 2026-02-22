namespace WebAPI_Basics.Dtos.Requests;

public class OrderResponse
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }=string.Empty;
}
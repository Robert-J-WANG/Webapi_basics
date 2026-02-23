namespace WebAPI_Basics.Dtos.Requests;

public class OrderQueryRequest
{
    public string? Status { get; set; }

    public string? SortBy { get; set; }   // amount / id
    public string? SortDir { get; set; }  // asc / desc

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
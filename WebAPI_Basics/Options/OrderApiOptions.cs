namespace WebAPI_Basics.Options;

public class OrderApiOptions
{
    public int DefaultPageSize { get; init; } = 20;
    public int MaxPageSize { get; init; } = 100;
}
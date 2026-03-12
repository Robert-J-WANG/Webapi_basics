namespace WebAPI_Basics.Dtos.Requests;

public class LoginRequest
{
    public string Username { get; init; } = "admin";
    public string Password { get; init; } = "123456";
}
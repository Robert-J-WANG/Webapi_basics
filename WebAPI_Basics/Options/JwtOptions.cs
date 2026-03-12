namespace WebAPI_Basics.Options;

public class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
    public int ExpireMinutes { get; init; } = 60;
}
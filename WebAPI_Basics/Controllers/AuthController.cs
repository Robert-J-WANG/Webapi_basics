using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebAPI_Basics.Dtos.Requests;
using WebAPI_Basics.Options;

namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(IOptions<JwtOptions> jwtOptions):ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(LoginRequest req )
    {
        
        // 最小演示：硬编码账号
        if (req.Username != "admin" || req.Password != "123456")
            return Unauthorized();

        var jwt = jwtOptions.Value;

        // 1) Claims（写进 token 的身份信息）
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, req.Username),
            new("role", "admin")
        };

        // 2) 生成签名凭据
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 3) 生成 JWT
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpireMinutes),
            signingCredentials: creds);

        // 4) 序列化成字符串给客户端
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new { access_token = tokenString, token_type = "Bearer" });
    } 
}
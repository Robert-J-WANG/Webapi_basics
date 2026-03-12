using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebAPI_Basics.Options;

namespace WebAPI_Basics.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new Exception("Jwt config section missing");;
        
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            if (string.IsNullOrEmpty(jwt.Key))
                throw new Exception("Jwt key config section missing");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                // 1) 验 issuer
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,

                // 2) 验 audience
                ValidateAudience = true,
                ValidAudience = jwt.Audience,

                // 3) 验签名
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),

                // 4) 验过期
                ValidateLifetime = true,

                // 允许一点点服务器时间偏差
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        
        return services;
    }
}
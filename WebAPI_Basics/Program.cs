
using Microsoft.EntityFrameworkCore;
using WebAPI_Basics.Data;
using WebAPI_Basics.Middlewares;
using WebAPI_Basics.Repositories;
using WebAPI_Basics.Services;

namespace WebAPI_Basics;

using Scalar.AspNetCore;

public class Program
{
    public static void Main(string[] args)
    {
        
        // --- 1. 实例化 WebApplicationBuilder ---
        
        // 初始化配置系统（Configuration）、日志工厂（Logging）及依赖注入容器（DI Container）。
        var builder = WebApplication.CreateBuilder(args);
       
        // --- 2. 服务注册阶段 (Service Configuration) ---
        
        // 将基于 Controller 的 MVC 架构服务添加到 DI 容器中，启用 Action 激活、模型绑定及验证等核心功能。
        builder.Services.AddControllers();
        
        // 注册我们自己的类型模型依赖
        builder.Services.AddScoped<OrderService>();
        // builder.Services.AddScoped<IOrderRepository,InMemoryOrderRepository > ();
        // builder.Services.AddScoped<IOrderRepository,SqlServerOrderRepository > ();
        builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
        // 注册数据库
        builder.Services.AddDbContext<AppDbContext>(options =>options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
        
        // 注册 Microsoft.AspNetCore.OpenApi 服务，利用 .NET 9 原生的元数据提取技术生成 OpenAPI 3.1 规范文档。
        builder.Services.AddOpenApi(); 

        // --- 3. 构建 WebApplication 实例 ---
        
        // 锁定服务容器，准备配置请求处理管道。
        var app = builder.Build();

        // --- 4. 请求管道配置阶段 (Middleware Pipeline) ---

        // 仅在开发环境（Development）中启用诊断和元数据工具，防止生产环境泄露系统拓扑结构。
        if (app.Environment.IsDevelopment())
        {
            // 映射 OpenAPI 规范文件的 JSON 终结点，默认路径为 /openapi/v1.json。
            app.MapOpenApi(); 

            // 集成 Scalar API 交互式引用中间件，渲染基于 OpenAPI 规范的可视化文档界面。
            app.MapScalarApiReference(options => 
            {
                options.WithTitle("WebAPI_Basics Documentation")
                    .WithTheme(ScalarTheme.Moon); 
            });
        }

        // 启用 HSTS 和 HTTPS 重定向中间件，强制执行传输层安全协议。
        app.UseHttpsRedirection();

        // 启用授权中间件（Authorization），拦截请求并校验声明（Claims）以进行访问控制。
        app.UseAuthorization();
        
        
        // 全局异常处理中间件：放在 MapControllers 之前，才能罩住 Controller/Service
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // 终结点路由映射：根据路由表将传入的 HTTP 请求分发至Controllers/ 目录下对应的 Action 方法。
        app.MapControllers();

        // --- 5. 启动应用 ---
        // 启动异步监听，进入事件循环。
        app.Run();
    }
}
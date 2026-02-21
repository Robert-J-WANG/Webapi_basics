## WEB API (Controller) 基础

### 1. 初始化项目

#### 1. 创建web api项目

- 选择使用controller
- 选择使用open api

#### 2. 运行项目

访问地址测试是否连接成功

```http
http://localhost:5085/weatherforecast/
```

```json
[
  {
    "date": "2026-02-22",
    "temperatureC": 13,
    "temperatureF": 55,
    "summary": "Bracing"
  },
  {
    "date": "2026-02-23",
    "temperatureC": 33,
    "temperatureF": 91,
    "summary": "Scorching"
  },
  {
    "date": "2026-02-24",
    "temperatureC": 35,
    "temperatureF": 94,
    "summary": "Balmy"
  },
  {
    "date": "2026-02-25",
    "temperatureC": 43,
    "temperatureF": 109,
    "summary": "Hot"
  },
  {
    "date": "2026-02-26",
    "temperatureC": -13,
    "temperatureF": 9,
    "summary": "Balmy"
  }
]
```

#### 3. 使用swagger

在 .NET 9 之后，微软默认**移除了内置的 Swashbuckle (Swagger)** 支持，转而推行官方的 **`Microsoft.AspNetCore.OpenApi`** 库。

- **官方库 (`Microsoft.AspNetCore.OpenApi`)**：它非常轻量，专门负责生成符合 OpenAPI 3.0/3.1 标准的 JSON 描述文件。

    **但它不带 UI 界面**（没有那个绿色的网页）

- Swashbuckle (Swagger) 这些是“皮肤”，负责读取官方库生成的 JSON 并展示出来

安装包：**Swashbuckle.AspNetCore**

DI容器注册swagger依赖

```c#
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
```

app使用

```c#
if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
```

**注意：要删除open api的依赖和使用，否则会和swagger冗余重读报错** 

完整代码：

```c#
namespace WebAPI_Basics;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
       
        builder.Services.AddControllers();
        // builder.Services.AddOpenApi(); // 移除 OpenApi()
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            // 移除 OpenApi()
            // app.MapOpenApi(); 
            // 使用 UseSwagger();
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
```

```c#
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
    </PropertyGroup>

    <ItemGroup>
<!--    移除OpenApi包的引用-->
<!--    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.12"/>-->
        <PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.4" />
    </ItemGroup>

</Project>

```

运行测试

```http
http://localhost:5085/swagger/
```

#### 4. 使用Scalar 

Scalar 比 swagger更现代， 界面更漂亮、支持多种编程语言的客户端代码生成，且与 .NET 9 官方库集成极简。

安装 NuGet 包

```BASH
dotnet add package Scalar.AspNetCore
```

在 `Program.cs` 中配置

```c#
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

        // 终结点路由映射：根据路由表将传入的 HTTP 请求分发至Controllers/ 目录下对应的 Action 方法。
        app.MapControllers();

        // --- 5. 启动应用 ---
        // 启动异步监听，进入事件循环。
        app.Run();
    }
}
```

运行测试

```http
http://localhost:5085/scalar/
```


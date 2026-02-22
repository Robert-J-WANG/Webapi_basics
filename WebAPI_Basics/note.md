## WEB API (Controller) 基础

### 1. 初始化项目

#### 1. 创建web api项目

- 选择使用controller
- 选择使用open api

#### 2. 主程序结构

项目入口文件是program.cs

```c#
using Microsoft.AspNetCore.Mvc;

// 实例化 WebApplicationBuilder
var builder = WebApplication.CreateBuilder(args);

// =======================
// 1) Services: 注册所有依赖（DI 容器）
// =======================

// 1.1 MVC / Controller 能力（Controller Web API 必备）
builder.Services.AddControllers();

// 1.2 API 文档（开发环境常用）
builder.Services.AddOpenApi();

// 1.3 自定的依赖（Service / Repository / DbContext / HttpClient ...）
// builder.Services.AddScoped<OrderService>();
// builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

// 1.4 常见可扩展项（先留位置，后面你要用再打开）
// builder.Services.AddDbContext<AppDbContext>(...);
// builder.Services.AddAuthentication(...);
// builder.Services.AddAuthorization(...);
// builder.Services.AddCors(...);

// 构建 WebApplication 实例
var app = builder.Build();

// =======================
// 2) Middleware: 配置请求处理管道（UseXXX 顺序很重要）
// =======================

// 2.1 开发环境工具（只在开发环境）
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 2.2 全局异常处理（真实项目非常推荐放最前面）
// app.UseExceptionHandler("/error");   // 或自定义异常中间件

// 2.3 安全相关（常见）
app.UseHttpsRedirection(); //http 重定向为 https

// 2.4 跨域（如果你有前端要访问 API，通常放在 Routing 之后、Auth 之前）
// app.UseCors("Default");

// 2.5 认证/授权（如果你做登录权限）
// app.UseAuthentication();
app.UseAuthorization();

// =======================
// 3) Endpoints: 映射端点（Controller 路由挂载）
// =======================
app.MapControllers();

// 运行app
app.Run();
```

总结：

- **Builder 阶段（准备/注册）**

    `CreateBuilder` + `Services.Add...`

    注册依赖、加载配置、准备日志

- **App 阶段（组装管道）**

    `Build` + `Use...` + `Map...`

    中间件有顺序顺序、路由端点映射

- **Run 阶段（启动监听）**

     `Run()`

    应用开始接 HTTP 请求

#### 3. 运行项目

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

#### 4. 使用swagger

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

代码如下：

```c#
public class Program
{
    public static void Main(string[] args)
    {
        ...
        // builder.Services.AddOpenApi(); // 移除 OpenApi
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            // app.MapOpenApi();  // 移除OpenApi
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        ...
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

#### 5. 使用Scalar 

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



### 2. 路由与 Action：把“方法调用”变成“HTTP 调用”

#### 1. 解决什么问题？

在 Console 项目里，调用订单查询是这样：

```c#
orderService.GetById(1);
```

但在 Web API 里，客户端不会直接调用你的 C# 方法，它只能发 HTTP 请求。

所以同样的查询动作，要变成端点动作（endpoint)：

```http
GET /orders
GET /orders/1
```

**问题来了：**

1. 请求进来后，**谁来接收**？

2. `GET` 和 `POST` 怎么区分？

3. `/orders/1` 里的 `1` 怎么传进 C# 方法参数？

    

#### 2. Controller、Action、Route

Controller 负责把请求动作按业务资源分类， 比如订单相关端点动作放在 `OrdersController`，用户相关端点动作放在 `UsersController，**Controller 是一个个不同的class。**

Action 是“具体的端点动作”，就是Controller 里的每一个 `public` 方法。 例如在 `OrdersController` 里：

- `GetAll()` 对应“查全部订单”
- `GetById(int id)` 对应“按 id 查订单”

Route 是“匹配规则”。Route 决定：某个 URL 请求是否能进入这个 Controller / Action。

**Controller 文件结构：**

```c#
[ApiController]          // 开启 API 模式
[Route("api/[controller]")] // 1. 基路径 (例如: /api/Weather)
public class WeatherController : ControllerBase
{
    [HttpGet]            // 2. 动词 -> 对应 URL: GET /api/Weather
    public IEnumerable<Data> Get() { ... }

    [HttpGet("today")]   // 3. 子路径 -> 对应 URL: GET /api/Weather/today
    public Data GetToday() { ... }
}
```

方括号 `[]` 被称为**特性（Attributes）**：

这些 `[ApiController]`、`[Route]`、`[HttpGet]` 等都定义在 **`Microsoft.AspNetCore.Mvc.Core`** 这个动态链接库（DLL）中。

当创建一个 Web API 项目时，.NET SDK 会自动引用这些库。

通过添加这些特性标识，框架能自动完成一些动作。比如自动模型验证，参数绑定，自动过滤非法格式请求等等。

controller中必须的3个特性标识：

- `[ApiController]`

    写在整个文件的最开始（`class` 关键字的上方），对**整个类**的声明。框架会自动处理 JSON 序列化、错误拦截和参数绑定。

- `[Route]（基路径）`

    是这个类里所有方法的**共有地址前缀**。通常建议写成 `[Route("[controller]")]`, 比如你的类叫 `OrderController`，那么访问这个类里任何方法的起始地址都是 `/Order`。

- `[HttpGet]` / `[HttpPost]` (动作约束)

    是对类里面**单个 Action 方法**的详细定义。 规定这个方法对应哪种“动作”（GET, POST, PUT, DELETE）。



#### 3. 创建OrdersController

先创建一个订单的controller， 包含2个上面提到的动作

```c#
using Microsoft.AspNetCore.Mvc;

namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController:ControllerBase
{
    private record OrderItem(int Id, decimal Amount, string Status);

    private static readonly List<OrderItem> Orders =
    [
        new(1, 100m, "Created"),
        new(2, 200m, "Paid")
    ];
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(Orders);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var order=Orders.FirstOrDefault(x=>x.Id==id);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }
}
```

一些解释：

- 继承内置的**ControllerBase基类**

- 方法的返回类型：**IActionResult**

    使用内置的统一功能的接口，通过调用内置的不同方法来返回相同格式的数据（响应码+响应结果(数据或者错误)）。

​	`Ok(Orders)`  - 表示返回 **200 OK**，并把对象序列化成 JSON。

​	`NotFound()` - 表示返回 **404 Not Found**

Web API 和 Console 的区别：

Console 可以返回 `null`；Web API 需要表达 **HTTP 语义**（200 / 404）。

**运行后的结果：**

- `GET /orders`  - 返回两条订单数据（200）

- `GET /orders/1` - 返回单条订单（200）

- `GET /orders/999` - 返回 404

- `GET /orders/abc` - 不会匹配到这个 Action（因为路由要求 `id:int`）



#### 4. 小结

从“方法调用思维”到“HTTP 接口思维”的第一步：

- 用 **Controller** 承接请求

- 用 **Route + HttpGet** 把 URL 映射到 Action

- 用 **路径参数绑定** 把 `/orders/1` 变成 `int id`

- 用 **HTTP 状态码** 表达结果（200 / 404）

**新的问题来了？？**

现在接口能跑了，但还有两个明显问题：

1. 返回的数据结构是临时写的 `OrderItem`，不适合作为正式 API 输出模型
2. 还不能创建订单（`POST /orders`），也就是说客户端的 JSON 请求体还没法进入 C# 对象



### 3. DTO 与模型绑定：从 JSON Body 变成 C# 对象

#### 1. 解决什么问题？

上一章我们已经能通过 URL 查询订单：

- `GET /orders`
- `GET /orders/{id}`

但订单接口还缺一个最基本能力：**创建订单**。

客户端创建订单时，通常会发送 JSON，例如：

```c#
{
  "amount": 99.9
}
```

问题来了：

1. 这个 JSON 谁来接？
2. 它怎么变成 C# 对象？
3. 返回给客户端的数据，能不能和内部数据结构分开？

这就是这一章要解决的核心：**输入（请求）与输出（响应）的结构设计**。

#### 2. 引入 DTO

当客户端发请求、接口返回结果时，和程序内部使用的对象，并不一定是同一种结构。

所以我们需要两类对象：

- **输入 DTO（Request DTO）**：表示客户端传进来的数据
- **输出 DTO（Response DTO）**：表示接口返回给客户端的数据

这样做的意义是：

- 接口结构更清晰（对外契约明确）
- 内部结构更容易调整（不直接暴露内部对象）
- 后面加验证、状态码、业务规则时会更稳定

#### 3. 为订单创建“输入 DTO”和“输出 DTO”

- 创建输入 DTO（客户端发来的 JSON）

    新建文件：`Dtos/Requests/OrderCreateRequest.cs`

    ```c#
    public class OrderCreateRequest
    {
        public decimal Amount { get; set; }
    }
    ```

    这个类表示客户端要创建订单时提交的数据： 当客户端发送 JSON body 时，框架会把它绑定成这个对象

- 创建输出 DTO（接口返回的数据）

    新建文件：`Dtos/Responses/OrderResponse.cs`

    ```c#
    public class OrderResponse
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
    ```

    这个类表示接口返回给客户端的订单结构。

- 模型绑定是怎么发生的？

    当 Action 参数里出现一个复杂类型（如 `OrderCreateRequest`）时，ASP.NET Core 会尝试从请求体（Body）读取 JSON，并自动绑定到这个对象。

    也就是说，客户端发送：

    ```c#
    {
      "amount": 99.9
    }
    ```

    Action 可以直接收到：

    ```c#
    request.Amount == 99.9m
    ```

    这一步就是“从 JSON Body 变成 C# 对象”。

#### 4.  OrdersController 中加入 `POST `

升级一下OrdersController：保留查询接口，并新增创建接口 ``POST /orders``。

```c#
namespace WebAPI_Basics.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController:ControllerBase
{
	...
    
    [HttpPost]
    public IActionResult Create(OrderCreateRequest  request)
    {
        var nextId=Orders.Count==0?1:Orders.Max(x=>x.Id)+1;
        var order = new OrderItem(nextId,request.Amount,"Created");
        
        Orders.Add(order);
        return Ok(ToResponse(order));
    }

    //辅助方法： 把OrderItem转换成OrderResponse
    private OrderResponse ToResponse(OrderItem order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            Amount = order.Amount,
            Status = order.Status,
        };
    }
    
}
```

**方法`POST /orders `**

表示：

- 路径：`/orders`
- 方法：`POST`

客户端发送 JSON body 后，框架会把它绑定成 `request`。

**输入和输出分开**

- 输入用 `OrderCreateRequest`
- 输出用 `OrderResponse`

Controller 不再直接把内部 `OrderItem` 返回给客户端，而是先转换成 `OrderResponse`。

**运行结果**

发送请求：

```c#
{
  "amount": 99.9
}
```

返回新订单（当前代码返回 200）

#### 5. 小结

这一章完成了 Web API 的关键一步：

- 能接收客户端的 JSON 请求体
- 能把 JSON 自动绑定成 C# 对象（模型绑定）
- 能用 DTO 区分“输入结构”和“输出结构”
- 能通过 `POST /orders` 创建资源

这意味着现在的api已经不只是“读”，而是开始具备真正的交互能力。

**新的问题来了？？**

现在 `POST /orders` 已经能创建订单，但返回结果还比较粗糙：

- 创建成功返回 `200` 还是 `201` 更合适？
- 创建后是否应该告诉客户端新资源地址（Location）？
- `GET /orders/{id}` 不存在时返回 `404`，创建失败时又该返回什么？

这些问题都属于 **HTTP 状态码与返回结果设计**，下一章就会系统解决。

### 4. 返回结果与状态码：让接口表达清楚“成功 / 失败”的含义

#### 1. 解决什么问题？

第3章里我们已经能创建订单，也能查询订单，但现在返回结果还不够规范：

- 创建成功时直接返回 `200 OK`
- 创建后没有告诉客户端“新资源在哪里”
- 输入不合理（例如 `amount <= 0`）时还没有明确的错误响应

也就是说，接口虽然能用，但 **HTTP 语义还不完整**。

这一章要解决的是：**让接口不仅返回数据，还返回正确的状态码和响应含义**。

#### 2. 为什么状态码很重要？

Web API 不只是“把数据吐出去”，它还要告诉客户端这次请求发生了什么：

- 成功查询到了 → `200 OK`
- 成功创建了 → `201 Created`
- 请求参数不合法 → `400 BadRequest`
- 资源不存在 → `404 NotFound`
- 当前状态不允许该操作 → `409 Conflict`（这一章先建立认识，后面会正式用到）

这样客户端（前端、移动端、其他服务）才能根据状态码做正确处理。

#### 3. 优化OrdersController

围绕当前 `OrdersController` 做三件事：

1. 把返回类型从 `IActionResult` 调整为 `ActionResult<T>`（让返回结果更清晰）

    **`ActionResult<T>` 是什么？为什么要用？**

    之前我们写的是接口：

    ```c#
    public IActionResult GetById(int id)
    ```

    这当然可以，但它只表达“返回一个动作结果”，没有表达“成功时返回的具体数据类型”。

    如果改成：

    ```c#
    public ActionResult<OrderResponse> GetById(int id)
    ```

    意思就更清楚了：

    - 成功时通常返回 `OrderResponse`
    - 失败时仍然可以返回 `NotFound()`、`BadRequest()` 等状态结果

    这会让接口签名更有表达力，也更利于接口文档展示。

2. `POST /orders` 使用 `201 Created`，并返回资源地址

    **为什么创建资源应该返回 `201 Created`？**

    当客户端调用 `POST /orders` 创建新订单时，这不是普通查询成功，而是**新资源被创建**。

    HTTP 语义里更合适的表达是：

    - **`201 Created`**
    - 并带上新资源地址（Location），例如 `/orders/3`

    ASP.NET Core 里常用写法是：

    ```c#
    CreatedAtAction(...)
    ```

    **CreatedAtAction**这个方法，它会自动返回：

    - 状态码 `201`
    - Location 头
    - 响应体（你传入的对象）

3. 对明显错误输入（`amount <= 0`）返回 `400 BadRequest`

    **输入错误如何表达？**

    创建订单时，如果金额小于等于 0，这种请求即使格式是合法 JSON，也不应该创建成功。

    例如：

    ```c#
    {
      "amount": 0
    }
    ```

    这种情况更适合返回：

    - `400 BadRequest`

    因为这是客户端提交的数据不符合接口要求。

优化升级后的代码：

```c#
[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    ...

    [HttpGet]
    public ActionResult GetAll()
    {
        var result = Orders.Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public ActionResult GetById(int id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(ToResponse(order));
    }

    [HttpPost]
    public ActionResult Create(OrderCreateRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than 0." });
        }

        var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
        var order = new OrderItem(nextId, request.Amount, "Created");
        Orders.Add(order);

        var response = ToResponse(order);
        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response
        );
    }
	...
}
```

#### 4. 关键的变化：

- 查询接口：成功返回 `200`，找不到返回 `404`

    `GetById` 现在明确表达了两种情况：

    - 找到 → `Ok(OrderResponse)`（200）
    - 找不到 → `NotFound()`（404）

    这就是“查询类接口”的基本语义。

- 创建接口：成功返回 `201 Created`

    `Create` 不再返回 `Ok(...)`，而是：

    ```c#
    return CreatedAtAction(
        nameof(GetById),
        new { id = response.Id },
        response);
    ```

    这段的含义是：

    - `nameof(GetById)`：告诉框架“新资源可以通过这个 Action 获取”
    - `new { id = response.Id }`：给这个 Action 提供路由参数
    - `response`：响应体内容

    最终客户端会得到：

    - `201 Created`
    - Location（指向 `/orders/{id}`） - 响应头里
    - 新创建的订单数据 - 响应体中

- 输入不合理：返回 `400 BadRequest`

    `amount <= 0` 的判断现在明确映射为 `400`：

    ```c#
    return BadRequest(new { message = "Amount must be greater than 0." });
    ```

    这一步开始建立“输入错误 ≠ 服务器错误”的意识。

- 另外，对返回的数据进行了`ToResponse `辅助方法的转换，对齐返回数据的类型。

#### 5. 小结

现在不仅能“写接口”，还能开始用 **HTTP 语义**表达业务结果：

- 查询成功 / 失败
- 创建成功
- 输入错误

这一步非常关键，因为 Web API 的专业感，很多时候就体现在状态码和返回结果是否清晰、稳定。

**新的问题来了？？**

现在 `amount <= 0` 是在 Action 里手写 `if` 判断。

如果以后字段变多（比如 `CustomerName`、`OrderType`、`Email`），每个接口都手写 `if` 会很快变乱。

因此，需要“参数是否合法”的基础校验的合理处理。

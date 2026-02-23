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
    public ActionResult<OrderResponse> GetAll()
    {
        var result = Orders.Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public ActionResult<OrderResponse> GetById(int id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(ToResponse(order));
    }

    [HttpPost]
    public ActionResult<OrderResponse> Create(OrderCreateRequest request)
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

### 5. 输入验证：框架自动处理基础校验

#### 1. 解决什么问题？

用 DataAnnotations + `[ApiController]` 让框架自动处理基础校验。

第4章里我们已经让 `POST /orders` 能返回更合理的状态码了，但还有一个明显问题：

创建订单时，金额是否合法，是在 Action 里手写判断：

```c#
if (request.Amount <= 0)
{
    return BadRequest(new { message = "Amount must be greater than 0." });
}
```

这在字段很少时还能接受，但如果后面请求字段变多（比如名称、邮箱、类型等），Controller 里会出现很多重复校验代码，接口会越来越乱。 

因此，需要解决的是：**把这类“输入是否合法”的基础校验交给框架处理，让 Controller 只关注接口动作本身。**

#### 2. 什么是输入验证？为什么放在 DTO 上？

创建订单时，客户端发来的 JSON 是“输入数据”。

这类数据先进入 `OrderCreateRequest`，所以最合适的校验位置就是 **输入 DTO**。

也就是说：

- “这个字段必填吗？”
- “这个数字范围是否合法？”

这些规则先写在 DTO 上，让框架在进入 Action 前先检查。

ASP.NET Core 常用做法是使用 **DataAnnotations（数据注解）**，例如：

- `[Required]`
- `[Range(...)]`

#### 3. `[ApiController]` 的作用

`[ApiController]` 在这里起什么作用？

在controller的最开始，我们已经添加过 

```c#
[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    ...
}       
```

它不只是一个标记，还会带来一个非常实用的行为：

当模型绑定完成后，如果 DTO 校验失败，框架会**自动返回 400 Bad Request**，并且不会继续执行 Action 方法。

这意味着：

- 不需要在每个 Action 里手写 `if` 检查基础字段合法性
- Controller 会更干净
- 错误响应格式也更统一（框架默认格式）

#### 4. 如何操作

只改两处：

1. 给 `OrderCreateRequest` 加验证注解

    ```c#
    using System.ComponentModel.DataAnnotations;
    
    namespace WebAPI.Dtos.Requests;
    
    public class OrderCreateRequest
    {
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }
    }
    ```

    **这里发生了什么？**

    - `[Range(...)]` 要求 `Amount` 必须大于 0
    - 如果客户端传 `0` 或负数，模型校验失败
    - 因为 Controller 上有 `[ApiController]`，框架会自动返回 400

2. 删除 `Create` Action 里手写的金额判断

    **只删除**这段：

    ```c#
    if (request.Amount <= 0)
    {
        return BadRequest(new { message = "Amount must be greater than 0." });
    }
    ```

    修改后的 `Create` 方法：

    ```c#
    [HttpPost]
    public ActionResult<OrderResponse> Create(OrderCreateRequest request)
    {
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
    ```

    请求进入 `Create(OrderCreateRequest request)` 之前，框架已经做了两件事：

    - **模型绑定**：把 JSON body 转成 `OrderCreateRequest`
    - **模型验证**：检查 `[Range]` 等注解规则是否满足

    如果验证失败，Action 根本不会执行到。所以删掉 `if (request.Amount <= 0)` 后，功能仍然成立，而且代码更干净。

#### 5. 小结

把“基础输入校验”从 Action 里移到了 DTO 上，并交给框架自动处理：

- 校验规则更集中（在 Request DTO）
- Controller 更干净
- 错误返回更统一

这一步非常关键，因为它让接口开始具备“可维护性”。

**新的问题来了？**

这一章虽然解决的是“输入格式/范围是否合法”，例如金额不能小于等于 0。

但还有一种错误不是输入格式问题，而是**业务规则问题**。

例如在订单场景里：

- 一个已经支付过的订单再次支付
- 当前状态不允许执行某个动作

这种错误即使输入格式完全正确，也应该失败，而且通常不是 `400`，而是更适合映射成 `409 Conflict` 等状态码。

这就需要对**业务错误与异常（把领域规则映射成 HTTP 响应）**的处理。

### 6. 业务错误与异常

把业务规则错误映射成正确的 HTTP 响应

#### 1. 解决什么问题？

上一章已经把“输入是否合法”交给框架处理了，例如：

- 金额必须大于 0 → 自动返回 `400`

但还有一类错误不属于输入格式问题，而是**业务规则不允许**。

在订单场景里最典型的例子就是：**重复支付**。

例如：

- 请求格式完全正确（`id` 是合法整数）
- 订单也存在
- 但订单状态已经是 `Paid`

这时候失败的原因不是“请求写错了”，而是“当前业务状态不允许这个动作”。

因此，我们需要解决的是：**如何在代码里表达这种业务错误，并把它返回成合适的 HTTP 状态码（例如 409）**

#### 2. 输入错误 vs 业务错误

先明确区分：输入错误 vs 业务错误。这一步很关键，因为它决定你返回什么状态码:

- 输入错误（第5章处理）

    客户端提交的数据本身不符合接口要求，例如：

    - `amount <= 0`
    - 缺字段
    - 类型不匹配

    这类问题通常是 `400 BadRequest`。

- 业务错误（本章处理）

    输入格式没问题，但当前业务规则不允许操作，例如：

    - 订单不存在（按当前接口语义，查询/动作目标不存在）
    - 订单已支付，不能再次支付

    这类问题应该映射成更准确的状态码，例如：

    - `404 NotFound`
    - `409 Conflict`

#### 3. 用“异常”来表达业务错误

为什么用“异常”来表达业务错误?

因为业务动作（比如支付）执行过程中，可能在多个地方失败：

- 查不到订单
- 状态不允许支付
- 以后还可能有更多规则

如果每一步都返回布尔值或字符串，Controller 很快会充满判断分支，逻辑会变乱。

用异常的方式可以把失败原因直接表达出来：

- `NotFoundException`
- `ConflictException`

然后在 Controller 中统一捕获，并翻译成 HTTP 响应。

这样结构会更清晰：

- 业务层负责“为什么失败”
- Controller 负责“失败时返回什么 HTTP 状态码”

#### 4. 使用 try catch 分层异常处理

以pay方法为例，可以使用 if + return 的形式处理异常， 比如：

```c#
if (order is null)
    return NotFound();
```

但是，业务逻辑里，不应该写 `return NotFound()` / `return Conflict()`， 这是HTTP响应的东西。

我们需要**建立“业务错误”和“HTTP响应”分离的意识**：

- 业务错误： 
    - 发现订单不存在 → `throw OrderNotFoundException`
    - 发现已支付 → `throw OrderConflictException`
- HTTP响应：
    - `return NotFound()` → 响应码404
    - `return Conflict()`→ 响应码409

因此，使用try catch语句进行分层：

- 在 `try` 里，代码站在“业务动作”的角度思考

    通过**throw**：表达“业务失败了，失败类型是什么”

- 在 `catch` 里，代码站在“Web API 响应”的角度思考

    **catch**：把这个失败翻译成“HTTP 响应是什么”

#### 5. 如何操作

在当前代码基础上加入“支付动作”和业务异常映射。

- 我们在现有 `OrdersController` 基础上增加一个支付接口：
    - `POST /orders/{id}/pay`
- 并加入两种业务错误映射：
    - 订单不存在 → `404`
    - 订单已支付 → `409`

#### 6. 代码实现

- 新增业务异常类型

    新建文件夹和文件：`Domain/OrderExceptions.cs`

    ```c#
    public class OrderNotFoundException(int id) : Exception($"Order with id {id} was not found.");
    
    public class OrderConflictException(string message) : Exception(message);
    ```

    使用新语法：主构造器 - 直接在类名中传递参数。

    先定义两个明确的异常类型来表达业务失败原因：

    - `OrderNotFoundException`
    - `OrderConflictException`

    后面 Controller 捕获时就能精准映射状态码。

- 在 `OrdersController` 中增加支付接口

    在第5章的 `OrdersController` 基础上，新增一个 Action（其余代码保持不变）：

    ```c#
    [HttpPost("{id:int}/pay")]
        public ActionResult<OrderResponse> Pay(int id)
        {
            try
            {
                var order = Orders.FirstOrDefault(x => x.Id == id);
                if (order == null)
                    throw new OrderNotFoundException(id);
                if (order.Status == "Paid")
                    throw new OrderConflictException("Order is already paid");
                order.Status = "Paid";
                
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
            catch (OrderConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    ```

    这里发生了什么变化？

    - 新增了一个动作型接口

        路径是：`POST /orders/{id}/pay`

        它表示对订单执行一个明确动作：**支付**。这和普通查询接口不同，因为它会改变订单状态。

    - 用异常表达业务失败原因

        使用try语句抛出异常，停止后续的代码执行。

        在支付过程中：

        - 找不到订单 → 抛 `OrderNotFoundException`
        - 已支付订单再次支付 → 抛 `OrderConflictException`

        这样主流程会更清楚：

        - 查订单
        - 判断状态
        - 修改状态
        - 返回结果

        失败分支通过异常表达，不和主流程混在一起。

    - 把业务错误映射成 HTTP 响应

        在 `catch` 中完成映射：

        - `OrderNotFoundException` → `404 NotFound`
        - `OrderConflictException` → `409 Conflict`

- 运行代码验证：

    - 支付一个未支付订单（成功）

        `200 OK`

        返回订单状态变为 `Paid`

    - 支付不存在的订单

        `404 NotFound`

    - 重复支付同一个订单

        `409 Conflict`

        返回错误信息（`Order is already paid.`）

#### 7. 小结

现在可以区分两类失败，并用不同方式处理：

- 输入校验失败 → 框架自动 `400`
- 业务规则失败 → 抛业务异常，再映射为 `404 / 409`

这让 API 从“能跑”进一步变成“语义清楚”。

**新的问题来了？**

现在 `Pay` 接口已经能处理业务错误，但会发现一个新问题：

- 支付逻辑、查询逻辑、创建逻辑都堆在 `OrdersController` 里
- Controller 开始变厚
- 后面动作一多，会越来越难维护

因此，我们需要把“HTTP 接口层”和“业务流程层”分开。

### 7. 引入service（业务）层

#### 1. 解决什么问题？

第6章我们已经完成了 `Pay` 动作，并且能把业务错误映射成正确的 HTTP 响应（`404 / 409`）。

功能上是对的，但代码结构开始出现一个明显问题：

`OrdersController` 里同时在做很多事情：

- 接收 HTTP 请求（路由、参数、返回状态码）
- 查找订单
- 判断业务规则（是否已支付）
- 修改订单状态

这会让 Controller 越来越厚。接口一多（创建、查询、支付、取消、退款……），Controller 很快就会变成“什么都写”的地方。

因此，我们需要 **把“HTTP 接口处理”和“业务流程处理”分开**。

#### 2. 为什么要引入 Service 层？

因为 Controller 和业务流程关注点不同。

**Controller 关注的是 HTTP**

- 路由是什么
- 参数怎么进来
- 返回 200 / 404 / 409 哪个状态码

**Service 关注的是业务流程**

以 `Pay` 为例，它关心的是：

- 查订单
- 判断是否允许支付
- 修改状态
- 返回处理结果（或抛出业务异常）

也就是说，Service 负责的是“这件事怎么做完”，而不是“HTTP 怎么表达”。

这样拆开之后，代码会更清晰：

- Controller 变薄（只做接口层工作）
- 业务逻辑集中（以后更容易复用和维护）

#### 3. 如何操作？

这一章我们只做结构调整，不改变已经实现的业务行为。

目标是把 `Pay` 的业务流程从 `OrdersController` 挪到 `OrderService`：

- `Controller` 仍然负责 `try/catch` 和返回 `404 / 409`
- `Service` 负责查订单、判断状态、修改状态、抛业务异常

这样第6章学到的“业务异常映射 HTTP”会保留，而且会变得更自然。

#### 4. 代码实现

- 新建 `OrderService`

    新建文件夹和文件：`Services/OrderService.cs`

    ```c#
    public class OrderService
    {
        public class OrderItem
        {
            public int Id { get; set; }
            public decimal Amount { get; set; }
            public string Status { get; set; } = string.Empty;
        }
    
        private static readonly List<OrderItem> Orders =
        [
            new OrderItem
            {
                Id = 1,
                Amount = 100m,
                Status = "Created"
            },
            new OrderItem
            {
                Id = 2,
                Amount = 200m,
                Status = "Paid"
            }
        ];
    
        public List<OrderItem> GetAll()
        {
            return Orders.ToList();
        }
    
        public OrderItem GetById(int id)
        {
            var order = Orders.FirstOrDefault(o => o.Id == id);
            if (order is null)
                throw new OrderNotFoundException(id);
            return order;
        }
    
        public OrderItem Create(OrderCreateRequest request)
        {
            var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
            var order = new OrderItem()
            {
                Id = nextId,
                Amount = request.Amount,
                Status = "Created"
            };
            Orders.Add(order);
            return order;
        }
    
        public OrderItem Pay(int id)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order is null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException($"Order {id} was already paid");
            order.Status = "Paid";
            return order;
        }
    }
    ```

    `OrderService` 现在负责四个业务动作：

    - `GetAll()`
    - `GetById(id)`
    - `Create(request)`
    - `Pay(id)`

    其中最关键的是 `Pay(id)`：

    - 查订单
    - 判断是否已支付
    - 修改状态
    - 抛业务异常（如果失败）

    这就是业务流程层的职责。

- 在 `Program.cs` 注册 Service（交给框架创建）

    Controller 里要使用 `OrderService`，就要先注册到依赖注入容器。

    在 `Program.cs` 里，`builder.Build();` 之前加入这一行：

    ```c#
    builder.Services.AddScoped<WebAPI.Services.OrderService>();
    ```

    **为什么要注册？**

    因为接下来我们会在 `OrdersController` 构造函数里写：

    ```c#
    public OrdersController(OrderService service)
    ```

    框架要想自动创建这个 Controller，就必须知道 `OrderService` 怎么创建。

    这就是依赖注入（DI）在 ASP.NET Core 里的落地方式。

- 修改 `OrdersController`，让它调用 Service

    在第6章的 `OrdersController` 基础上做修改：

    - 删除 Controller 里的 `OrderItem` 和 `Orders` 列表（这些移到 Service 了）
    - 增加构造函数注入 `OrderService`
    - 各个 Action 改为调用 `_service`

    文件：`Controllers/OrdersController.cs`

    ```c#
    [ApiController]
    [Route("[controller]")]
    public class OrdersController(OrderService service) : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<OrderResponse>> GetAll()
        {
            var result = service.GetAll().Select(ToResponse).ToList();
            return Ok(result);
        }
    
        [HttpGet("{id:int}")]
        public ActionResult<OrderResponse> GetById(int id)
        {
            try
            {
                var order = service.GetById(id);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
        }
    
        [HttpPost]
        public ActionResult<OrderResponse> Create(OrderCreateRequest request)
        {
            var order = service.Create(request);
            var response = ToResponse(order);
    
            return CreatedAtAction(
                nameof(GetById),
                new { id = response.Id },
                response
            );
        }
    
        [HttpPost("{id:int}/pay")]
        public ActionResult<OrderResponse> Pay(int id)
        {
            try
            {
                var order = service.Pay(id);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
            catch (OrderConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    
    
        //辅助方法： 把OrderItem转换成OrderResponse
        private OrderResponse ToResponse(OrderService.OrderItem order)
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

    

- 代码结构发生了什么变化？

    **Controller 变薄了**

    它现在主要做的是：

    - 接收请求参数
    - 调用 `service`
    - 把结果转成 `OrderResponse`
    - 把异常映射成 `404 / 409`

    **Service 集中了业务流程**

    Controller 不再直接操作订单列表，也不再写支付规则判断。
     这些都集中到了 `OrderService` 中。

    这就是“分层”的第一步。

#### 5. 用 Scalar 测试

接口行为第6章保持一致：

- `GET /orders` → 200
- `GET /orders/{id}` → 存在返回 200，不存在返回 404
- `POST /orders`（合法）→ 201
- `POST /orders`（非法 amount）→ 400（仍由 `[ApiController]` + DTO 验证处理）
- `POST /orders/{id}/pay` → 成功 200；不存在 404；重复支付 409

#### 6. 小结

本章完成了一个非常关键的结构升级：

- **Controller**：负责 HTTP 接口层
- **Service**：负责业务流程层

这会让后面的代码更容易扩展，也为进一步拆分“数据访问”做准备。

**新的问题来了？**

现在虽然业务流程已经从 Controller 移到了 Service，但是：

- 当前阶段 `OrderService` 内部使用嵌套类型 `OrderItem`
- `OrdersController` 使用辅助方法 `ToResponse(OrderService.OrderItem order)` 做 DTO 映射时，耦合了这个类型。

 `OrderItem` 已经被 Controller 使用，说明它不再只是 Service 内部细节。

**因此， 需要先把订单模型从 Service 内部独立出来，理顺跨层数据传递边界。**

### 8. 引入model（模型）层

#### 1. 解决什么问题？

第7章引入 `OrderService` 后，Controller 里的业务流程代码已经变少了，但代码里出现了一个新的结构问题：

- `OrderItem` 实际上已经不只是 `OrderService` 的内部细节，而是变成了在不同层之间传递的数据模型。

当一个模型已经被多个位置使用时，把它放在 `OrderService` 内部就不太合适了。

因此：需要把订单模型从 `OrderService` 内部独立出来，形成统一的 `Order` 模型，让代码结构更清晰、类型依赖更自然。

#### 2. 模型职责

先明确模型的边界：

- DTO（接口输入输出模型）

    用于 HTTP 层（Controller 和客户端通信）：

    - `	OrderCreateRequest`
    - `OrderResponse`

    它们的职责是：**接口契约**，不是业务层的通用模型。

- 定义一个Order（业务层共享模型）

    用于当前阶段的跨层传递：

    - Controller（映射前）
    - Service
    - 存储（后面可能会出现）

    它的职责是：**在应用内部表示订单数据**。

#### 3. 如何操作？

只做“模型收口”，不改业务行为，不改状态码，不改异常处理逻辑。

改动顺序：

1. 新增 `Domain/Order.cs`
2. `OrderService` 去掉嵌套 `OrderItem`，改用 `Order`
3. `OrdersController` 的 `ToResponse(...)` 改参数类型
4. 验证行为不变

#### 4. 代码实现

- 定义统一的订单模型 `Order`

    新建文件：`Domain/Order.cs`

    ```c#
    public class Order
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
    ```

    **为什么这里用 `Order` 这个名字？**

    因为这一章调整的是“订单模型”的位置与职责。

    它已经不是 `OrderService` 里的一个内部临时类型，而是明确表示“订单数据”的模型，所以直接用 `Order` 更自然。

- 修改 `OrderService`（去掉嵌套 `OrderItem`），使用公共的Order

    ```c#
    public class OrderService
    {
        private static readonly List<Order> Orders =
        [
            new Order
            {
                Id = 1,
                Amount = 100m,
                Status = "Created"
            },
            new Order
            {
                Id = 2,
                Amount = 200m,
                Status = "Paid"
            }
        ];
    
        public List<Order> GetAll()
        {
            return Orders.ToList();
        }
    
        public Order GetById(int id)
        {
            var order = Orders.FirstOrDefault(o => o.Id == id);
            if (order is null)
                throw new OrderNotFoundException(id);
            return order;
        }
    
        public Order Create(OrderCreateRequest request)
        {
            var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
            var order = new Order()
            {
                Id = nextId,
                Amount = request.Amount,
                Status = "Created"
            };
            Orders.Add(order);
            return order;
        }
    
        public Order Pay(int id)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order is null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException($"Order {id} was already paid");
            order.Status = "Paid";
            return order;
        }
    }
    ```

    ```c#
    public class OrderService
    {
        private static readonly List<Order> Orders =
        [
            new Order
            {
                Id = 1,
                Amount = 100m,
                Status = "Created"
            },
            new Order
            {
                Id = 2,
                Amount = 200m,
                Status = "Paid"
            }
        ];
    
        public List<Order> GetAll()
        {
            return Orders.ToList();
        }
    
        public Order GetById(int id)
        {
            var order = Orders.FirstOrDefault(o => o.Id == id);
            if (order is null)
                throw new OrderNotFoundException(id);
            return order;
        }
    
        public Order Create(OrderCreateRequest request)
        {
            var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
            var order = new Order()
            {
                Id = nextId,
                Amount = request.Amount,
                Status = "Created"
            };
            Orders.Add(order);
            return order;
        }
    
        public Order Pay(int id)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order is null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException($"Order {id} was already paid");
            order.Status = "Paid";
            return order;
        }
    }
    ```

​	`OrderItem`不再适合作为长期跨层模型, Service 对外返回的“订单”已经有了稳定位置（`Domain`）

- 修改 `OrdersController` 的响应转换方法

    ```c#
    //辅助方法： 把OrderItem转换成OrderResponse
        private OrderResponse ToResponse(Order order)
        {
            return new OrderResponse
            {
                Id = order.Id,
                Amount = order.Amount,
                Status = order.Status,
            };
        }
    ```

- **代码结构有什么变化？**

    表面上看只是改了类型名和类型位置，但结构上更清楚了：

    - Controller 不再依赖 `OrderService` 内部嵌套类型
    - `Order` 成为统一的订单模型
    - `OrderCreateRequest` / `OrderResponse` 仍然只负责接口输入输出

#### 5. 用 Scalar 测试

接口行为应该与上一章保持一致。用 Scalar 测试这些接口即可：

- `GET /orders`
- `GET /orders/{id}`
- `POST /orders`
- `POST /orders/{id}/pay`

#### 6. 小结

这一章完成了一个关键收口：

- 把已经跨层使用的订单模型从 `OrderService` 内部提出来
- 用统一的 `Order` 模型替代 `OrderService.OrderItem`
- 保持 Controller 的响应映射写法一致（`ToResponse(Order order)`）

这样后面的代码会更清晰，也更容易继续扩展。

**新的问题来了？**

现在 `OrderService` 的业务流程已经比较清楚，但它还在直接维护内存列表并处理数据读写。

也就是说，Service 里同时放着两类职责：

- 业务流程
- 数据存取

因此需要对Service再分层，只保留业务流程

### 9. 引入repository（存取）层

#### 1. 解决什么问题？

前面的章节，我们已经把 `OrdersController` 里的业务流程挪到了 `OrderService`，结构明显更清楚了：

- Controller 负责 HTTP
- Service 负责业务流程

但 `OrderService` 里还在直接操作内存列表（查找、添加、更新订单）。

这说明 Service 里仍然混着两类职责：

1. 业务流程（创建、支付、规则判断）
2. 数据存取（查、存、改）

这一章要解决的是：**把“数据存取”单独抽出来，交给 Repository**。

这样 Service 就只关心业务流程，不关心数据存在哪里、怎么取出来。

#### 2. 为什么要引入 Repository？

因为“业务流程”和“数据存取”变化的原因不同。

- Service 会因为业务规则变化而改

    例如：

    - 支付前要增加更多判断
    - 创建订单时增加默认逻辑

- 数据存取会因为存储方式变化而改

    例如：

    - 现在是内存列表
    - 以后可能换数据库（EF Core）

如果这两类代码写在一起，后面换存储方式时会影响业务流程代码。

把数据访问抽出来后，Service 只依赖一个“存取接口”，就更稳定。

#### 3. 如何操作？

这章做三件事：

1. 定义 `IOrderRepository`（存取接口）
2. 实现 `InMemoryOrderRepository`（内存版）
3. 修改 `OrderService`：不再直接操作列表，改为调用 Repository

Controller 的职责和行为保持不变。

#### 4. 代码实现

1. 定义 Repository 接口（先把“需要什么能力”说清楚）

    新建文件夹和文件：`Repositories/IOrderRepository.cs`

    ```c#
    public interface IOrderRepository
    {
        List<Order> GetAll();
        Order? GetById(int id);
        Order Add(decimal amount);
        void UpdateStatus(int id, string status);
    }
    ```

    **这里为什么这样设计？**

    先看它提供的四个能力：

    - `GetAll()`：查询全部
    - `GetById(id)`：按 id 查询
    - `Add(amount)`：新增订单
    - `UpdateStatus(id, status)`：更新状态

    也就是说，Repository 只负责“存取动作”，不负责判断“能不能支付”。

     “能不能支付”仍然是 Service 的业务规则。

    这一点很关键：**Repository 是数据层，不是业务层**。

2. 实现内存版 Repository

    新建文件：`Repositories/InMemoryOrderRepository.cs`

    ```c#
    public class InMemoryOrderRepository : IOrderRepository
    {
        private static readonly List<Order> Orders =
        [
            new Order
            {
                Id = 1,
                Amount = 100m,
                Status = "Created"
            },
            new Order
            {
                Id = 2,
                Amount = 200m,
                Status = "Paid"
            }
        ];
    
        public List<Order> GetAll() => Orders.ToList();
    
        public Order? GetById(int id) => Orders.FirstOrDefault(x => x.Id == id);
    
        public Order Add(decimal amount)
        {
            var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
            var order = new Order()
            {
                Id = nextId,
                Amount = amount,
                Status = "Created"
            };
            Orders.Add(order);
            return order;
        }
    
        public void UpdateStatus(int id, string status)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order is null)
                return;
            order.Status = status;
        }
    }
    ```

    数据存取职责在哪里？

    现在订单列表已经从 `OrderService` 挪到了 `InMemoryOrderRepository` 里。

    这意味着：

    - Service 不再直接操作 `List`
    - Service 只通过接口 `IOrderRepository` 取数据、存数据

    这就是“数据存取抽离”的核心。

3. 修改 `OrderService`，改为依赖 Repository

    - 删除内部 `Orders` 列表
    - 构造函数注入 `IOrderRepository`
    - 所有数据操作改为调用 `repo`

    ```c#
    public class OrderService(IOrderRepository repo)
    {
        public List<Order> GetAll() => repo.GetAll();
    
        public Order GetById(int id) => repo.GetById(id) ?? throw new OrderNotFoundException(id);
    
        public Order Create(OrderCreateRequest request) => repo.Add(request.Amount);
    
        public Order Pay(int id)
        {
            var order = repo.GetById(id);
            if (order is null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException($"Order {id} was already paid");
            repo.UpdateStatus(id, "Paid");
            return order;
        }
    }
    ```

    **这里的逻辑是怎么分工的？**

    Repository 负责数据存取

    - 查订单
    - 新增订单
    - 更新状态

    Service 负责业务规则与流程

    以 `Pay(id)` 为例：

    1. 通过 `_repo.GetById(id)` 查订单
    2. 不存在 → 抛 `OrderNotFoundException`
    3. 已支付 → 抛 `OrderConflictException`
    4. 调用 `_repo.UpdateStatus(...)` 更新状态
    5. 返回更新后的结果

    这样业务流程和数据存取就拆开了。

4. 在 `Program.cs` 注册 Repository

    已经注册了 `OrderService`， 现在需要再注册 Repository，让框架知道 `IOrderRepository` 用哪个实现类。

    ```c#
     builder.Services.AddScoped<IOrderRepository,InMemoryOrderRepository > ();
    ```

    **为什么这里是“接口 + 实现”的注册方式？**

    因为 Service 依赖的是 `IOrderRepository`（抽象），不是 `InMemoryOrderRepository`（具体实现）。

    这样做的意义是：

    - 当前用内存实现
    - 以后换数据库实现时，Service 不用改，只要换注册映射即可

    **这就是接口抽象的价值。**

5. `OrdersController` 要不要改？

    **不用改。**

    这是这章非常重要的结果：

    我们改了 Service 和数据层，但 Controller 的接口行为不需要变化。

    这说明分层是有效的：

    - 上层（Controller）不需要知道底层存储细节怎么实现

#### 5. 用 Scalar 测试

接口行为应该和第8章保持一致（这是本章检查重点）：

- `GET /orders` → 200
- `GET /orders/{id}` → 存在 200，不存在 404
- `POST /orders`（合法）→ 201
- `POST /orders`（非法 amount）→ 400
- `POST /orders/{id}/pay` → 成功 200；不存在 404；重复支付 409

#### 6. 小结

完成了四层结构的基本拆分：

- **Controller**：HTTP 接口层
- **Service**：业务流程层
- **Model** : 数据模型层
- **Repository**：数据存取层（当前是内存实现）

这一步非常关键，因为它让后面切换数据库时不需要推倒重写业务流程。

**新的问题来了？**

现在 Repository 和 Service 已经拆开了，但还有一个问题：

- `OrdersController` 能拿到 `OrderService`
- `OrderService` 能拿到 `IOrderRepository`

这些对象是谁创建的？为什么会自动注入？

我们在 `Program.cs` 里写了 `AddScoped(...)`，但还没有系统解释它背后的规则。



### 10. DI：注册与注入（把对象创建交给框架）

#### 1. 要解决什么问题？

我们已经把核心代码拆成了三层：

- Controller
- Service
- Repository

并且在 `Program.cs` 里写了这样的注册代码：

```c#
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<IOrderRepository,InMemoryOrderRepository > ();    
```

同时，`OrdersController` 和 `OrderService` 的构造函数里也写了依赖参数：

- `OrdersController(OrderService service)`
- `OrderService(IOrderRepository repo)`

问题来了：

1. 这些对象是谁创建的？
2. 为什么我没有 `new OrderService(...)`，它却能用？
3. `AddScoped(...)` 到底在做什么？

这一章要解决的就是：**ASP.NET Core 中依赖注入（DI）的基本工作方式**。

#### 2. DI 的用途

如果不用 DI，就需要再在 Controller 里手动创建依赖：

```c#
var repo = new InMemoryOrderRepository();
var service = new OrderService(repo);
```

这样会带来两个问题：

- 上层代码知道太多底层细节（耦合高）
- 对象创建到处都是，后面越来越难维护

DI 的做法是：

- 只声明“我需要什么”（构造函数参数）
- 框架负责“怎么创建并传给你”

也就是说：

- `OrdersController` 只说：我需要 `OrderService`
- `OrderService` 只说：我需要 `IOrderRepository`
- 框架根据 `Program.cs` 的注册规则，把对象串起来创建好

#### 3. `builder.Services.AddScoped(...)` 到底在做什么？

这行代码的本质是：**向 DI 容器注册“类型映射规则”**。

例子1：注册具体类型

```c#
builder.Services.AddScoped<OrderService>();
```

意思是：

- 容器里登记一个规则：`OrderService` 可以被创建和注入

例子2：注册接口到实现类的映射

```
builder.Services.AddScoped<IOrderRepository,InMemoryOrderRepository>();
```

意思是：

- 当有人需要 `IOrderRepository` 时
- 实际给它一个 `InMemoryOrderRepository`

这就是为什么 `OrderService` 构造函数里写的是接口：

```
public OrderService(IOrderRepository repo)
```

但运行时仍然能拿到具体对象。

#### 4. “注入”是怎么发生的？（结合你当前代码）

**第一步：请求进来，框架要创建 `OrdersController`**

框架看到 Controller 构造函数：

```
public OrdersController(OrderService service)
```

它就知道：创建 `OrdersController` 之前，得先准备一个 `OrderService`。

**第二步：框架去容器里找 `OrderService` 的注册规则**

你在 `Program.cs` 里已经注册了：

```
builder.Services.AddScoped<OrderService>();
```

所以框架知道它可以创建 `OrderService`。

但创建 `OrderService` 时，框架又发现它的构造函数需要 `IOrderRepository`。

**第三步：继续解析 `IOrderRepository`**

框架再去容器里找：

```
builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
```

找到了，就创建 `InMemoryOrderRepository`，再传给 `OrderService`，最后把 `OrderService` 传给 `OrdersController`。

这就是依赖注入在运行时的基本过程。

#### 5. 控制反转（IoC）

以前是自己写：

- `new Repo()`
- `new Service(repo)`
- `new Controller(service)`

现在变成：

- 只声明依赖
- 框架按注册规则创建对象并注入

对象创建的控制权从自己的业务代码转移到了框架（容器），这就是 IoC 的直观含义。

#### 6. 为什么构造函数注入是最常见方式？

因为它有三个优点：

- 依赖一眼可见 - 看构造函数参数就知道这个类需要什么。
- 依赖是“必须的” - 没有这些依赖，对象就不能被正确创建。
- 更容易测试 - 以后做单元测试时，可以传入假的 Repository（Mock/Fake）来测试 Service。

#### 7. “生命周期”设置

`Scoped / Transient / Singleton` 是什么？

它们是 DI 注册时的“生命周期”设置，决定对象在多长范围内复用。

当前先建立基础认知即可：

- `AddTransient` - 每次要用时都创建一个新对象：

    - 要一次，给一次新的
    - 不复用

- `AddScoped` - 同一个请求范围内复用同一个对象。

    - 一次 HTTP 请求里如果多次用到同一种服务，通常会复用
    - 下一个请求再创建新的

    **这是 Web API 里很常用的生命周期。**

- `AddSingleton`  -  整个应用启动后只创建一次，后面一直复用同一个对象。
    - 全局单例
    - 生命周期最长

**为什么我们当前用 `AddScoped`？**

现在的 `OrderService` 和 `Repository` 都是和“请求处理流程”密切相关的组件。

使用 `Scoped` 的好处是：

- 生命周期和一次请求比较匹配
- 不会像 Singleton 那样长期共享同一个实例（更容易引出状态问题）
- 也不像 Transient 那样每次解析都创建新对象（通常没必要）

在后面接触数据库（例如 EF Core DbContext）时，会更清楚为什么很多组件默认是 Scoped。

#### 8. 小结

这一章解释当前项目里对象是怎么串起来的：

- Controller 不是手动 `new`
- Service / Repository 也不是随便出现的
- 它们是由 DI 容器根据 `Program.cs` 的注册规则自动创建并注入的

这一步非常重要，因为后面加更多层（日志、配置、数据库上下文）都离不开这套机制。

**新的问题来了？**

现在结构已经比较清楚了，但所有方法仍然是同步写法：

- `GetById(...)`
- `Create(...)`
- `Pay(...)`

当前用内存列表问题不大，但真实项目里很多操作都会涉及 IO（数据库、网络调用），如果还用同步方式，会影响吞吐和响应能力。

这就需要**Async Web API：把同步接口改成 `async` + `Task`**。



### 11. Async Web API

#### 1. 要解决什么问题？

前面几章我们已经把结构搭起来了：

- Controller 负责 HTTP
- Service 负责业务流程
- Repository 负责数据存取
- DI 负责对象创建与注入

但目前所有方法还是同步写法，例如：

- `GetById(...)`
- `Create(...)`
- `Pay(...)`

现在用内存列表时问题不明显，因为内存操作很快，也不是 IO。

但真实项目里常见的是：

- 查数据库
- 调第三方接口
- 发网络请求

这些都是 **IO 操作**。如果还用同步写法，会在等待期间占着线程，影响并发处理能力。

这一章要解决的是：**把当前接口改成异步写法，建立 Web API 的 async/await 基本结构**。

#### 2. 为什么要改成异步？

- 同步写法的问题（在真实 IO 场景）
    - 同步方法在等待数据库/网络返回时，会一直占着当前线程。
    - 请求多了之后，线程资源会更紧张，吞吐会变差。
- 异步写法的作用
    - 异步不是“让代码更快执行”，而是让线程在等待 IO 时可以先去处理别的请求。
    - 这样整体并发能力更好。

所以这章的重点不是性能测试，而是先把结构改对：

- 方法签名改为 `Task` / `Task<T>`
- Controller 使用 `async/await`
- Service / Repository 也同步升级为异步版本

#### 3. 如何操作？

这章只做一类改动：**把同步方法签名和调用链改成异步**，业务行为保持不变。

改动顺序按调用链走最清楚：

1. Repository 接口改成异步
2. InMemory Repository 实现改成异步
3. Service 改成异步
4. Controller 改成异步
5. 行为验证（接口结果应保持不变）

#### 4. 代码实现

- 修改 `IOrderRepository`（异步签名）

    文件：`Repositories/IOrderRepository.cs`

    把同步方法改成 `Task` / `Task<T>`：

    ```c#
    public interface IOrderRepository
    {
        Task<List<Order>> GetAllAsync();
        Task<Order?> GetByIdAsync(int id);
        Task<Order> AddAsync(decimal amount);
        Task UpdateStatusAsync(int id, string status);
    }
    ```

    这里发生了什么变化？

    - 原来的 `GetAll()` → `GetAllAsync()`
    - 返回值从 `T` 变成 `Task<T>`
    - 无返回值方法从 `void` 变成 `Task`

​	命名上加 `Async` 是常见约定，方便一眼识别异步方法。

- 修改 `InMemoryOrderRepository`（实现异步接口）

    文件：`Repositories/InMemoryOrderRepository.cs`

    当前还是内存操作，没有真实 IO，所以这里的“异步”主要是为了建立统一接口形态。

    实现上可以用 `Task.FromResult(...)` 和 `Task.CompletedTask`。

    ```c#
    public class InMemoryOrderRepository : IOrderRepository
    {
        private static readonly List<Order> Orders =
        [
            new Order
            {
                Id = 1,
                Amount = 100m,
                Status = "Created"
            },
            new Order
            {
                Id = 2,
                Amount = 200m,
                Status = "Paid"
            }
        ];
    
        public Task<List<Order>> GetAllAsync() => Task.FromResult(Orders.ToList());
    
        public Task<Order?> GetByIdAsync(int id) => Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
    
        public Task<Order> AddAsync(decimal amount)
        {
            var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
            var order = new Order()
            {
                Id = nextId,
                Amount = amount,
                Status = "Created"
            };
            Orders.Add(order);
            return Task.FromResult(order);
        }
    
        public Task UpdateStatusAsync(int id, string status)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order != null)
            {
                order.Status = status;
            }
            return Task.CompletedTask;
        }
    }
    ```

    **为什么这里没有 `await`？**

    - 因为当前实现是内存操作，没有真正的异步 IO。
    - 这一层改成异步签名，是为了让调用链（Service → Controller）形成统一模式，后面换数据库时更自然。
    - 也就是说，这章是在建立“异步接口形态”，不是假装内存操作变快。

- 修改 `OrderService`（异步化业务流程）

    文件：`Services/OrderService.cs`

    把方法改成 `Task` / `Task<T>`，并在调用 Repository 时使用 `await`。

    ```c#
    public class OrderService(IOrderRepository repo)
    {
        public async Task<List<Order>> GetAllAsync() => await repo.GetAllAsync();
    
        public async Task<Order> GetByIdAsync(int id) => await repo.GetByIdAsync(id) ?? throw new OrderNotFoundException(id);
    
        public async Task<Order> CreateAsync(OrderCreateRequest request) => await repo.AddAsync(request.Amount);
    
        public async Task<Order> PayAsync(int id)
        {
            var order = await repo.GetByIdAsync(id);
            if (order is null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException($"Order {id} was already paid");
            await repo.UpdateStatusAsync(id, "Paid");
            return order;
        }
    }
    ```

    **这里要注意什么？**

    业务逻辑没有变化，变化的是“等待方式”：

    - 原来直接调用 `_repo.GetById(...)`
    - 现在改成 `await _repo.GetByIdAsync(...)`

    这说明异步改造的目标是**调用链形态升级**，不是改业务规则。

    

- 修改 `OrdersController`（Action 改成 async）

    文件：`Controllers/OrdersController.cs`

    在上个版本基础上，把四个 Action 改成异步写法，并调用 `service.*Async(...)`。

    ```c#
    [ApiController]
    [Route("[controller]")]
    public class OrdersController(OrderService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<OrderResponse>>> GetAll()
        {
            var result = (await service.GetAllAsync()).Select(ToResponse).ToList();
            return Ok(result);
        }
    
        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderResponse>> GetById(int id)
        {
            try
            {
                var order = await service.GetByIdAsync(id);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
        }
    
        [HttpPost]
        public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request)
        {
            var order = await service.CreateAsync(request);
            var response = ToResponse(order);
    
            return CreatedAtAction(
                nameof(GetById),
                new { id = response.Id },
                response
            );
        }
    
        [HttpPost("{id:int}/pay")]
        public async Task<ActionResult<OrderResponse>> Pay(int id)
        {
            try
            {
                var order = await service.PayAsync(id);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
            catch (OrderConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    
    
        //辅助方法： 把OrderItem转换成OrderResponse
        private OrderResponse ToResponse(Order order)
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

**关键变化是什么？**

1. Action 返回类型变成 `Task<...>`

    例如：`ActionResult<OrderResponse>` → `Task<ActionResult<OrderResponse>>`

    因为 Action 内部要 `await` 异步操作。

2. Service / Repository 方法名加 `Async`

    这是异步方法命名约定，能让调用链更清晰。

3. 业务行为不变，结构形态升级

#### 5. 用 Scalar 测试

接口行为应与上一章一致：

- `GET /orders` → 200
- `GET /orders/{id}` → 存在 200，不存在 404
- `POST /orders`（合法）→ 201
- `POST /orders`（非法 amount）→ 400
- `POST /orders/{id}/pay` → 成功 200；不存在 404；重复支付 409

#### 小结

已经把当前 Web API 从同步调用链升级为异步调用链：

- Controller `async/await`
- Service `Task` / `Task<T>`
- Repository `Task` / `Task<T>`

这为后面接数据库、网络调用打好了结构基础。

**新的问题来了？**

现在已经是异步调用链了，但还有一个现实问题：

如果客户端中途取消请求（比如页面关闭、用户停止请求），这个取消信号如何传到 Controller、Service、Repository？

这就需要对 **请求取消的传递** 处理。



### 12. CancellationToken

#### 1. 要解决什么问题？

把“请求取消”从 Controller 传到 Service / Repository.

上一章我们已经把接口改成了异步调用链：

- Controller → `async/await`
- Service → `Task`
- Repository → `Task`

但异步还差一个很重要的现实问题：**请求被取消怎么办？**

例如：

- 用户在页面上发起请求后马上关闭页面
- 客户端主动取消请求
- 网关/代理中断了连接

这时候如果服务端还继续执行后续逻辑（尤其是数据库/网络 IO），就会造成不必要的资源消耗。

这一章要解决的是：**把请求取消信号（CancellationToken）沿着调用链传下去**，形成正确的结构。

#### 2. 什么是 CancellationToken？它在这里扮演什么角色？

`CancellationToken` 可以理解为一个“取消信号”。

- 请求还在继续 → token 未取消
- 客户端中断请求 → token 可能变为已取消

在 ASP.NET Core 中，Action 参数里可以直接接收 `CancellationToken`。

框架会把当前 HTTP 请求关联的取消信号传进来。

也就是说，Controller 能拿到“请求是否被取消”的信号，然后把它继续传给 Service / Repository。

#### 3. 调用链形态

在当前这个项目里，Repository 还是内存实现，没有真实数据库 IO，所以很难看到明显取消效果。

重点是建立正确的调用链形态：

- Controller 接收 `CancellationToken`
- Service 方法签名接收 `CancellationToken`
- Repository 方法签名接收 `CancellationToken`
- 调用时把 token 一路传下去

后面换数据库或外部 HTTP 调用时，这个结构就能直接用上。

#### 4. 如何操作

改动顺序仍然按调用链走：

1. `IOrderRepository` 增加 `CancellationToken` 参数
2. `InMemoryOrderRepository` 实现同步更新
3. `OrderService` 增加 `CancellationToken` 参数并继续传递
4. `OrdersController` Action 接收 `CancellationToken` 并传给 Service

业务行为保持不变。

#### 5. 代码实现

1. 修改 `IOrderRepository`（增加 token 参数）

    文件：`Repositories/IOrderRepository.cs`

    在异步签名基础上，为每个方法增加 `CancellationToken cancellationToken` 参数：

    ```c#
    public interface IOrderRepository
    {
        Task<List<Order>> GetAllAsync(CancellationToken cancellationToken);
        Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken);
        Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken);
    }
    ```

    **为什么 Repository 也要接收 token？**

    因为真正可能耗时的操作通常发生在 Repository（数据库）或外部调用层。

    即使当前是内存实现，也应该先把接口形态设计好，这样后面换实现时不需要再改 Controller / Service 的方法签名。

2. 修改 `InMemoryOrderRepository`（接收并传递 token）

    文件：`Repositories/InMemoryOrderRepository.cs`

    当前是内存操作，没有真实异步 IO。这里主要做两件事：

    - 方法签名加上 `CancellationToken`
    - 在方法开始处可选地调用 `cancellationToken.ThrowIfCancellationRequested()`（演示结构）

    ```c#
    public class InMemoryOrderRepository : IOrderRepository
    {
        private static readonly List<Order> Orders =
        [
            new Order
            {
                Id = 1,
                Amount = 100m,
                Status = "Created"
            },
            new Order
            {
                Id = 2,
                Amount = 200m,
                Status = "Paid"
            }
        ];
    
        public Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Orders.ToList());
        }
    
        public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
        }
    
    
        public Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextId = Orders.Count == 0 ? 1 : Orders.Max(x => x.Id) + 1;
            var order = new Order()
            {
                Id = nextId,
                Amount = amount,
                Status = "Created"
            };
            Orders.Add(order);
            return Task.FromResult(order);
        }
    
        public Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order != null)
            {
                order.Status = status;
            }
            return Task.CompletedTask;
        }
    }
    ```

    **这里的 `ThrowIfCancellationRequested()` 是什么作用？**

    它会在 token 已取消时抛出 `OperationCanceledException`，表示当前操作应当停止。

    在这一章里，它主要是帮助你建立认知：

    - token 不只是“传递着玩”
    - 它可以在合适位置被检查并终止流程

    当前是基础版，不需要在 Controller 里专门处理这个异常。

3. 修改 `OrderService`（接收 token 并继续传递）

    文件：`Services/OrderService.cs`

    把方法签名加上 `CancellationToken cancellationToken`，并传给 Repository。

    ```c#
    using WebAPI_Basics.Domain;
    using WebAPI_Basics.Dtos.Requests;
    using WebAPI_Basics.Repositories;
    
    namespace WebAPI_Basics.Services;
    
    public class OrderService(IOrderRepository repo)
    {
        public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken) =>
            await repo.GetAllAsync(cancellationToken);
    
        public async Task<Order> GetByIdAsync(int id, CancellationToken cancellationToken) =>
            await repo.GetByIdAsync(id, cancellationToken) ?? throw new OrderNotFoundException(id);
    
        public async Task<Order> CreateAsync(OrderCreateRequest request, CancellationToken cancellationToken) =>
            await repo.AddAsync(request.Amount, cancellationToken);
    
        public async Task<Order> PayAsync(int id, CancellationToken cancellationToken)
        {
            var order = await repo.GetByIdAsync(id, cancellationToken);
            if (order is null)
                throw new OrderNotFoundException(id);
            if (order.Status == "Paid")
                throw new OrderConflictException($"Order {id} was already paid");
            await repo.UpdateStatusAsync(id, "Paid", cancellationToken);
            return order;
        }
    }
    ```

    **这里的关键点是什么**？

    Service 这一层目前没有复杂取消逻辑，它的职责是：

    - 接收 token
    - 调用下层时继续传递 token

    这一步很重要，因为很多项目里取消信号就是在中间层被“断掉”的。

    一旦断掉，底层即使支持取消也用不上。

4. 修改 `OrdersController`（从 Action 参数接收 token）

    文件：`Controllers/OrdersController.cs`

    为每个 Action 增加 `CancellationToken cancellationToken` 参数，并传给 `service`。

    ```c#
    [ApiController]
    [Route("[controller]")]
    public class OrdersController(OrderService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<OrderResponse>>> GetAll(CancellationToken cancellationToken)
        {
            var result = (await service.GetAllAsync(cancellationToken)).Select(ToResponse).ToList();
            return Ok(result);
        }
    
        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken cancellationToken)
        {
            try
            {
                var order = await service.GetByIdAsync(id, cancellationToken);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
        }
    
        [HttpPost]
        public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request,CancellationToken cancellationToken)
        {
            var order = await service.CreateAsync(request, cancellationToken);
            var response = ToResponse(order);
    
            return CreatedAtAction(
                nameof(GetById),
                new { id = response.Id },
                response
            );
        }
    
        [HttpPost("{id:int}/pay")]
        public async Task<ActionResult<OrderResponse>> Pay(int id, CancellationToken cancellationToken)
        {
            try
            {
                var order = await service.PayAsync(id,cancellationToken);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
            catch (OrderConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    
        //辅助方法： 把OrderItem转换成OrderResponse
        private OrderResponse ToResponse(Order order)
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

    **这里有一个你容易忽略但很重要的点：**

    - `CreatedAtAction(nameof(GetById), ...)` 仍然可以用

    虽然 `GetById` 现在多了一个 `CancellationToken` 参数，但 `CreatedAtAction` 仍然只需要传路由参数：

    ```csharp
    new { id = response.Id }
    ```

    因为 `CancellationToken` 不是路由参数，它是框架在运行时提供的请求取消信号，不参与 URL 路由匹配。

#### 6. 用 Scalar 测试

在当前内存实现下，接口行为应该和上一章保持一致：

- `GET /orders` → 200
- `GET /orders/{id}` → 存在 200，不存在 404
- `POST /orders`（合法）→ 201
- `POST /orders`（非法 amount）→ 400
- `POST /orders/{id}/pay` → 成功 200；不存在 404；重复支付 409

这章的重点不是功能变化，而是把取消信号正确地接入和传递。

#### 7. 小结

把“请求取消”纳入了调用链设计：

- Controller 能接收请求级 `CancellationToken`
- Service 能继续传递 token
- Repository 接口和实现也支持 token

这为后面接数据库、HTTP 客户端等真实 IO 场景打下基础。

**新的问题来了？**

现在的 API 已经具备了：

- 基础 CRUD 中的读/建
- 动作端点（Pay）
- 状态码语义
- 验证、异常、分层、DI、异步、取消传递

但查询能力还比较弱：`GET /orders` 目前只能返回全部订单，无法按条件筛选，也不能分页。

因此，还需要处理：**Query 参数：过滤 / 排序 / 分页**。

### 13. Query 参数

#### 1. 要解决什么问题？

给 `GET /orders` 加上过滤 / 排序 / 分页。

现在的API 已经能完成基础查询、创建和支付动作，但 `GET /orders` 还有一个明显问题：

它现在只能“全部返回”。

这在数据很少时没问题，但一旦订单变多，就会出现几个实际需求：

- 只看已支付订单（过滤）
- 按金额排序（排序）
- 一次只看一页（分页）

这一章要解决的是：**让 `GET /orders` 接收查询参数（Query String），并在服务层中完成基础过滤 / 排序 / 分页**。

#### 2. 什么是 Query 参数？它和路由参数有什么区别？

前面已经用过路由参数：

- `GET /orders/1`（`id` 在路径里）

Query 参数，写在 `?` 后面，例如：

- `GET /orders?status=Paid`
- `GET /orders?sortBy=amount&sortDir=desc`
- `GET /orders?page=1&pageSize=20`

**区别可以这样理解**：

- **路由参数**：定位“哪一个资源”（例如哪个订单 id）
- **Query 参数**：描述“如何查询列表”（筛选、排序、分页）

所以 `GET /orders` 的查询能力，天然适合放在 Query 参数里。

#### 3. 如何操作

这章做三件事：

1. 定义一个查询 DTO：`OrderQueryRequest`（接收 query 参数）
2. 修改 `OrdersController.GetAll(...)`：从 Query 接收条件
3. 修改 `OrderService.GetAllAsync(...)`：用 LINQ 做过滤 / 排序 / 分页

Repository 暂时不改（仍然返回全部数据给 Service 处理）。

#### 4. 代码实现

1. 新增查询 DTO（接收 Query 参数）

    新建文件：`Dtos/Requests/OrderQueryRequest.cs`

    ```c#
    public class OrderQueryRequest
    {
        public string? Status { get; set; }
    
        public string? SortBy { get; set; }   // amount / id
        public string? SortDir { get; set; }  // asc / desc
    
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
    ```

    **为什么要用 DTO 接收 Query？**

    因为查询条件很快就会变多。

    如果全写在 Action 参数里，会变成这样：

    ```c#
    GetAll(string? status, string? sortBy, string? sortDir, int page = 1, int pageSize = 20, CancellationToken ct = default)
    ```

    参数一多，接口签名会越来越乱。

    用 `OrderQueryRequest` 的好处是：

    - 查询条件集中在一个对象里
    - 后面扩展字段更方便
    - 和你前面“输入 DTO”的思路一致

2. 修改 `OrdersController.GetAll(...)`，从 Query 接收参数

    文件：`Controllers/OrdersController.cs`

    在上一版本基础上，**只修改 `GetAll` 这个 Action**：

    - 增加 `OrderQueryRequest query`
    - 用 `[FromQuery]` 明确告诉框架从 Query String 绑定
    - 调用 `service.GetAllAsync(query, cancellationToken)`， 把query对象传给service

    ```c#
    [HttpGet]
        public async Task<ActionResult<List<OrderResponse>>> GetAll([FromQuery] OrderQueryRequest query,
            CancellationToken cancellationToken)
        {
            var result = (await service.GetAllAsync(query, cancellationToken)).Select(ToResponse).ToList();
            return Ok(result);
        }
    ```

    **这里发生了什么？**

    当客户端请求：

    - `GET /orders?status=Paid&page=1&pageSize=10`

    框架会自动把 Query 参数绑定到 `OrderQueryRequest`：

    - `query.Status == "Paid"`
    - `query.Page == 1`
    - `query.PageSize == 10`

    这就是 Query 参数的模型绑定。

3. 修改 `OrderService.GetAllAsync(...)`，用 LINQ 实现查询能力

    文件：`Services/OrderService.cs`

    ```c#
    public class OrderService(IOrderRepository repo)
    {
        public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken)
        {
            var orders = await repo.GetAllAsync(cancellationToken);
            
            // 根据query参数，对数据集处理
            // 1) 过滤（status）
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                orders = orders.Where(o => string.Equals(o.Status, query.Status, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
    
            // 2) 排序（sortBy + sortDir）
            var sortBy = query.SortBy?.Trim().ToLowerInvariant();
            var sortDir = query.SortDir?.Trim().ToLowerInvariant();
    
            var desc = sortDir == "desc";
    
            // switch语句 (新语法)
            orders = sortBy switch
            {
                "amount" => desc
                    ? orders.OrderByDescending(x => x.Amount).ToList()
                    : orders.OrderBy(x => x.Amount).ToList(),
    
                "id" => desc
                    ? orders.OrderByDescending(x => x.Id).ToList()
                    : orders.OrderBy(x => x.Id).ToList(),
    
                _ => orders.OrderBy(x => x.Id).ToList() // 默认按 Id 升序
            };
    
            // 还原后的老式写法（语句块）
            /*
            switch (sortBy)
            {
                case "amount":
                    if (desc)
                    {
                        orders = orders.OrderByDescending(x => x.Amount).ToList();
                    }
                    else
                    {
                        orders = orders.OrderBy(x => x.Amount).ToList();
                    }
                    break;
    
                case "id":
                    if (desc)
                    {
                        orders = orders.OrderByDescending(x => x.Id).ToList();
                    }
                    else
                    {
                        orders = orders.OrderBy(x => x.Id).ToList();
                    }
                    break;
    
                default:
                    orders = orders.OrderBy(x => x.Id).ToList(); // 默认按 Id 升序
                    break;
            }
            */
    
            // 3) 分页（page + pageSize）
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
            pageSize = Math.Min(pageSize, 100); // 基础保护，避免一次拿太多
    
            orders = orders
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            return orders;
        }
        ....
    }        
    ```

    **这里的 LINQ 逻辑是怎么串起来的？**

    1. 先拿全部数据

        ```csharp
        var items = await _repo.GetAllAsync(cancellationToken);
        ```

        Repository 仍然只负责“取数据”，查询规则由 Service 负责。

    2. 过滤（Where）

        如果传了 `status`，就按状态筛选：

        - `status=Paid`
        - `status=Created`

        使用 `StringComparison.OrdinalIgnoreCase`，这样大小写不敏感（`paid` / `PAID` 也能匹配）。

    3. 排序（OrderBy / OrderByDescending）

        通过 `sortBy` + `sortDir` 控制排序：

        - `sortBy=amount&sortDir=desc`
        - `sortBy=id&sortDir=asc`

        如果没传或传了不支持的字段，默认按 `Id` 升序。

    4. 分页（Skip / Take）

        通过：

        - `page`
        - `pageSize`

        把结果切成一页一页：

        - `Skip((page - 1) * pageSize)`
        - `Take(pageSize)`

        并做了基础保护：

        - `page < 1` → 视为 `1`
        - `pageSize < 1` → 用默认值 `20`
        - `pageSize > 100` → 限制为 `100`

    

#### 5. 用 Scalar 测试

1. 不带参数（默认行为）

    `GET /orders`

    预期：

    - 仍然返回列表
    - 默认按 `Id` 升序
    - 默认分页（第1页，20条；你当前数据量很小，看起来和以前一样）

2. 按状态过滤

    - `GET /orders?status=Paid`

    预期：

    - 只返回 `Paid` 状态订单

3. 按金额降序排序

    - `GET /orders?sortBy=amount&sortDir=desc`

    预期：

    - 金额高的在前面

4. 分页参数

    - `GET /orders?page=1&pageSize=1`
    - `GET /orders?page=2&pageSize=1`

    预期：

    - 每页只返回 1 条
    - 第1页和第2页结果不同

5. 组合查询（最接近真实用法）

    - `GET /orders?status=Paid&sortBy=amount&sortDir=desc&page=1&pageSize=10`

    预期：

    - 先过滤，再排序，再分页

#### 6. 小结

让列表接口具备了基础查询能力：

- **过滤**（Where）
- **排序**（OrderBy / OrderByDescending）
- **分页**（Skip / Take）
- **Query 参数模型绑定**（`[FromQuery]` + DTO）

这一步非常重要，因为真实项目里列表接口几乎都会有类似需求。

**新的问题来了？**

现在功能已经比较完整，但随着内容增多，文件会越来越多：

- Controller
- Service
- Repository
- Dtos/Requests
- Dtos/Responses
- Domain

如果没有统一结构和命名约定，项目会很快变乱。

因此，需要对项目结构与命名的约定。

### 14. 项目结构与命名约定

#### 1. 解决什么问题？

让代码在继续增长时不乱

Order Web API 已经具备了完整的基础能力：

- 路由与 Action
- DTO 与模型绑定
- 状态码语义
- 输入验证
- 业务异常映射
- Controller / Service / Repository 分层
- DI
- async/await + CancellationToken
- Query 参数（过滤 / 排序 / 分页）

功能已经能跑起来了，但这时会出现另一个问题：**项目规模一变大，代码很容易乱**。

典型表现是：

- 新文件不知道放哪
- 命名风格前后不一致
- Controller 里混入 DTO / 业务类
- 以后回头看代码时定位困难

这一章要解决的是：**给当前项目建立一套稳定、容易扩展的文件结构与命名约定**。

这样后面继续加数据库、认证、日志时，代码不会失控。

#### 2. 对齐当前项目里的角色

现在已经有这些角色（概念上）：

- **Controller**：处理 HTTP 请求与响应
- **Service**：处理业务流程
- **Repository**：处理数据存取
- **DTO**：定义接口输入输出结构
- **Domain**：领域概念（异常、状态、后面还会有实体/枚举等）

这些角色在项目目录里应该有清晰位置，命名也应该保持一致。

#### 3. 推荐目录结构

```text
WebAPI/
├─ Controllers/
│  └─ OrdersController.cs
├─ Services/
│  └─ OrderService.cs
├─ Repositories/
│  ├─ IOrderRepository.cs
│  └─ InMemoryOrderRepository.cs
├─ Dtos/
│  ├─ Requests/
│  │  ├─ OrderCreateRequest.cs
│  │  └─ OrderQueryRequest.cs
│  └─ Responses/
│     └─ OrderResponse.cs
├─ Domain/
│  └─ OrderExceptions.cs
├─ Program.cs
├─ appsettings.json
└─ WebAPI.csproj
```

**为什么这样分目录？**

##### `Controllers/`

放所有 API 入口类，例如：

- `OrdersController`

这里只处理：

- 路由
- 参数绑定
- 调用 Service
- 返回 HTTP 响应

不放业务流程细节，不放数据存取代码。

##### `Services/`

放业务流程类，例如：

- `OrderService`

这里负责：

- 业务动作编排（Create / Pay）
- 业务规则判断
- 抛业务异常

不负责 HTTP 状态码，不直接关心路由。

##### `Repositories/`

放数据存取抽象与实现，例如：

- `IOrderRepository`
- `InMemoryOrderRepository`

这里负责：

- 查询
- 新增
- 更新

不负责业务规则（例如“是否允许支付”）。

##### `Dtos/Requests` 与 `Dtos/Responses`

把接口输入输出分开存放：

- `Requests/`：客户端传入的数据结构
- `Responses/`：接口返回给客户端的数据结构

你现在已经有：

- `OrderCreateRequest`
- `OrderQueryRequest`
- `OrderResponse`

这样分开之后，后面字段增多时不会混乱。

##### `Domain/`

放领域相关概念。你当前已经有：

- `OrderExceptions.cs`

后面如果继续扩展，还可能放：

- `OrderStatus`（枚举）
- `Order`（领域实体）
- 领域规则相关类型

这样可以避免把领域概念散落在 Controller / Service 中。

#### 4. 命名约定：为什么要统一？

结构解决的是“放哪里”，命名解决的是“看名字就知道它是什么”。

如果命名不统一，文件再分目录也会乱。

**当前项目可以采用的命名约定：**

##### 1）Controller：`资源名 + Controller`

- `OrdersController`

表示一组订单相关接口入口。

##### 2）Service：`资源名 + Service`

- `OrderService`

表示订单业务流程服务。

##### 3）Repository

- 接口：`IOrderRepository`
- 实现：`InMemoryOrderRepository`

规则很清晰：

- 接口前缀 `I`
- 实现类名字体现存储方式（`InMemory`）

以后换数据库实现时很自然，例如（概念上）：

- `EfOrderRepository`

##### 4）Request DTO：`动作/用途 + Request`

你当前已经用的是：

- `OrderCreateRequest`
- `OrderQueryRequest`

看到名字就知道：

- 是请求模型
- 用于哪个场景（Create / Query）

##### 5）Response DTO：`资源名 + Response`

- `OrderResponse`

表示接口返回的订单结构。

##### 6）异步方法名：加 `Async`

- `GetAllAsync`
- `GetByIdAsync`
- `CreateAsync`
- `PayAsync`

#### 5. 补全API功能

在统一规则的结构和文件命名前提下，对api的CRUD功能里的更新和删除功能补全

- 删除 order

    删除一个order，并不是销毁，而是从数据列表中移除

- 更新 order

    更新一个order，可以修改订单的属性，一般id不能改

- 代码实现

    ```c#
    public interface IOrderRepository
    {
        ...
        Task<int> DeleteAsync(int id, CancellationToken cancellationToken);
        Task<Order?> UpdateAsync(Order order, CancellationToken cancellationToken);
    }
    ```

    ```c#
    public class InMemoryOrderRepository : IOrderRepository
    {
       ...
    
        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order != null)
            {
                Orders.Remove(order);
            }
            return Task.FromResult(id);
        }
    
        public Task<Order?> UpdateAsync(Order newOrder, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = Orders.FirstOrDefault(x => x.Id == newOrder.Id);
            if (order != null)
            {
                order.Amount= newOrder.Amount;
                order.Status=  newOrder.Status;
            }
            return Task.FromResult(order);
        }
    }
    ```

    ```c#
    public class OrderUpdateQueryRequest
    {
        public decimal Amount { get; set; }
        public string Status { get; set; }="Created";
    }
    ```

    ```c#
    public class OrderService(IOrderRepository repo)
    {
        ...
        public async Task<int > DeleteAsync(int id, CancellationToken cancellationToken)
        {
            var order = await repo.GetByIdAsync(id, cancellationToken);
            if (order is null)
                throw new OrderNotFoundException(id);
            await repo.DeleteAsync(order.Id, cancellationToken);
            return order.Id;
        }
    
        public async Task<Order> UpdateAsync(int id, OrderUpdateQueryRequest query, CancellationToken cancellationToken)
        {
            var order = await repo.GetByIdAsync(id, cancellationToken);
            if (order is null)
                throw new OrderNotFoundException(id);
            order.Amount = query.Amount;
            order.Status = query.Status;
            return order;
        }
    }
    ```

    ```c#
    [ApiController]
    [Route("[controller]")]
    public class OrdersController(OrderService service) : ControllerBase
    {
        ...
    
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<int>> Delete(int id, CancellationToken cancellationToken)
        {
            try
            {
                await service.DeleteAsync(id, cancellationToken);
                return Ok(new { Message = $"order {id} is deleted。" });
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
        }
    
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Order>> Update(int id, OrderUpdateQueryRequest query,
            CancellationToken cancellationToken)
        {
            try
            {
                var order = await service.UpdateAsync(id, query, cancellationToken);
                return Ok(ToResponse(order));
            }
            catch (OrderNotFoundException)
            {
    
                return NotFound();
            }
        }
    	...
    }
    ```

    

#### 6. 小结

到这里，完成了Controller Web API（基础版）

- 从项目启动到 Controller
- 从路由到 DTO
- 从状态码到验证
- 从异常到分层
- 从 DI 到 async/cancellation
- 从列表查询到项目结构

后面继续推进进阶内容方向（数据库、认证、日志、中间件等）。




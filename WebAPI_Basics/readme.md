# Controller Web API 

## 第1部分：从 Console 思维迁移到 Web API 思维

### 第1章 开工：创建 Web API 项目与“跑通第一个 Controller”

**要解决的问题**：我怎么把项目跑起来？Controller 放哪？请求怎么进来？
**产出**：

- 创建 ASP.NET Core Web API 项目（`net9.0`）
- 理解默认生成的基础项目结构（`Program.cs`、`appsettings.json`、`Controllers/`）
- 跑通一个最简单的 `PingController` / `HealthController`
- 使用 OpenAPI 文档端点 + Scalar 进行接口查看与测试（按你当前路线）

**引出下一章**：项目能跑了，但还只是“通路测试”；下一步要把“方法调用”变成真正的 HTTP 接口（路由与 Action）。

------

### 第2章 路由与 Action：把“方法调用”变成“HTTP 调用”

**要解决的问题**：`GET/POST` 对应哪个 Action？`[Route]` / `[HttpGet]` 怎么写？
**产出**：

- `OrdersController`
- `GET /orders`
- `GET /orders/{id}`
- 理解路由模板、路由参数约束（如 `{id:int}`）

**引出下一章**：读接口有了，但创建订单时数据从哪里来？需要引入输入输出模型（DTO）与模型绑定。

------

## 第2部分：输入输出（DTO）与状态码（HTTP 语义）

### 第3章 DTO 与模型绑定：从 JSON Body 变成 C# 对象

**要解决的问题**：请求 Body 的 JSON 怎么变成 C# 对象？返回给客户端的数据结构如何控制？
**产出**：

- `OrderCreateRequest`（输入 DTO）
- `OrderResponse`（输出 DTO）
- `POST /orders` 创建订单（先用内存数据）
- 理解模型绑定（Body → DTO）

**引出下一章**：接口能创建订单了，但成功/失败应该怎么表达？需要用正确的 HTTP 状态码说清楚。

------

### 第4章 返回结果与状态码：Ok / Created / BadRequest / NotFound / Conflict

**要解决的问题**：API 应该怎么返回 200 / 201 / 400 / 404 / 409？
**产出**：

- `ActionResult<T>` / `IActionResult` 的基本使用
- `CreatedAtAction(...)`（201 + Location）
- `GET id 不存在 → 404`
- 手写基础 `BadRequest(...)` 和 `Conflict(...)`

**引出下一章**：现在有些校验还是手写 if，不够统一；下一步把基础输入校验交给框架。

------

## 第3部分：验证与异常（基础但必须）

### 第5章 输入验证：DataAnnotations + `[ApiController]` 的自动 400

**要解决的问题**：不想每个 Action 手写一堆 if 校验；让框架帮我处理基础参数错误。
**产出**：

- 在 `OrderCreateRequest` 上使用 `[Required]`、`[Range]` 等注解
- 理解 `[ApiController]` 的自动验证行为
- 验证失败自动返回 `400`

**引出下一章**：输入格式错误能自动处理了，但“业务规则错误”怎么办？比如重复支付不是输入格式错，而是业务冲突。

------

### 第6章 业务错误与异常：把领域规则映射成 404 / 409

**要解决的问题**：业务规则失败（如订单不存在、重复支付）如何表达，并返回正确 HTTP 响应？
**产出**：

- 定义业务异常（如 `OrderNotFoundException`、`OrderConflictException`）
- 在 Controller 中用 `try/catch` 做异常映射
- `Pay` 用例：
    - 不存在 → `404`
    - 重复支付 → `409`
- 理解 `throw`（表达业务失败）与 `catch`（翻译 HTTP 响应）的分工

**引出下一章**：Controller 里已经开始出现业务流程代码和异常处理逻辑，下一步需要把业务流程移到 Service 层，让 Controller 变薄。

------

## 第4部分：分层结构（Controller → Service → Repository）

### 第7章 组织代码：引入 Service 层（应用服务）

**要解决的问题**：Controller 既处理 HTTP，又处理业务流程，代码会越来越厚。
**产出**：

- 新建 `OrderService`
- 将 Create / Get / Pay 的业务流程移动到 Service
- Controller 只负责：
    - 接收请求
    - 调用 Service
    - 返回 HTTP 结果
- 当前阶段 `OrderService` 内部先使用嵌套类型 `OrderService.OrderItem`（作为过渡）
- `OrdersController` 使用辅助方法 `ToResponse(OrderService.OrderItem order)` 做 DTO 映射

**引出下一章**：`OrderItem` 已经被 Controller 使用，说明它不再只是 Service 内部细节；在继续引入 Repository 之前，需要先把订单模型从 Service 内部独立出来，理顺跨层数据传递边界。

------

### 第8章 统一订单模型：把 `OrderService.OrderItem` 提升为独立的 `Domain.Order`

**要解决的问题**：第7章中 `OrderItem` 已成为 Controller 与 Service 之间传递的数据模型，但它仍定义在 `OrderService` 内部（`OrderService.OrderItem`），后续引入 Repository 会导致模型位置与依赖方向不合理。
**产出**：

- 新增 `Domain/Order.cs`
- 将 `OrderService.OrderItem` 移出 Service，改为 `Domain.Order`
- `OrderService` 改为返回 `Order` / `List<Order>`
- `OrdersController` 的辅助方法改为 `ToResponse(Order order)`
- 明确模型职责边界：
    - DTO：`OrderCreateRequest / OrderQueryRequest / OrderResponse`（HTTP 输入输出）
    - `Domain.Order`：业务层与仓储层共享模型（第一阶段当前版本）

**引出下一章**：模型边界理顺后，再拆 Repository，就不会出现“Repository 依赖 Service 内部类型”的问题，分层结构才能更稳。

------

### 第9章 Repository：内存实现 InMemoryRepository

**要解决的问题**：Service 里仍然直接维护数据集合与数据读写逻辑，职责还不够清晰。
**产出**：

- `IOrderRepository`（或 `IRepository<Order>`）
- `InMemoryOrderRepository`
- Repository 负责 `GetAll / GetById / Add / Update`
- Service 通过 Repository 完成业务流程（Create / Pay）
- 使用统一的 `Domain.Order` 作为 Service / Repository 之间的传递模型

**引出下一章**：Repository 和 Service 都拆出来了，但对象是谁创建的？怎么自动注入到 Controller？需要框架 DI。

------

## 第5部分：依赖注入在 ASP.NET Core 中的落地

### 第10章 框架里的 DI：注册与注入（Transient / Scoped / Singleton 的基础用法）

**要解决的问题**：Controller / Service / Repository 怎么自动创建与注入？`Program.cs` 里注册的服务都是什么？
**产出**：

- 在 `Program.cs` 注册依赖：
    - `AddScoped<OrderService>()`
    - `AddScoped<IOrderRepository, InMemoryOrderRepository>()`
- Controller 构造器注入 `OrderService`
- 理解“同一个 DI 容器里的不同来源”：
    - 框架注册（如 `AddControllers`）
    - 第三方注册（如你项目里使用的 OpenAPI/文档相关扩展）
    - 自定义业务注册（Service / Repository）
- 生命周期基础认知（先讲常用原则）

**引出下一章**：结构完整了，但方法还是同步写法；真实项目里数据库/网络 IO 需要异步，下一步升级 async/await 调用链。

------

## 第6部分：异步化与取消（Web API 基础能力）

### 第11章 Async Web API：把同步接口改成 `async` + `Task`

**要解决的问题**：真实项目中的 IO（数据库/网络）不能长期用同步写法，否则影响并发处理能力。
**产出**：

- Repository / Service / Controller 改为异步签名（`Task` / `Task<T>`）
- Controller Action 使用 `async/await`
- 统一异步命名约定（`...Async`）
- 在当前内存实现下先建立“异步调用链形态”，业务行为保持不变

**引出下一章**：有了异步调用链后，请求中途被取消（客户端断开）时怎么办？需要把取消信号传下去。

------

### 第12章 CancellationToken：请求取消的传递

**要解决的问题**：客户端取消请求时，如何把取消信号从 Controller 传到 Service / Repository？
**产出**：

- Action 参数接收 `CancellationToken`
- Service / Repository 方法签名增加 `CancellationToken`
- 调用链中一路传递 token
- 在内存实现中演示基础检查（如 `ThrowIfCancellationRequested()`）
- 理解这一章重点是“传递结构”，不是复杂取消逻辑

**引出下一章**：接口已经能查、建、支付，但列表查询能力还不够；下一步给 `GET /orders` 增加过滤 / 排序 / 分页。

------

## 第7部分：查询能力（LINQ + Query 参数）

### 第13章 Query 参数：过滤 / 排序 / 分页

**要解决的问题**：`GET /orders` 目前只能返回全部订单，不能按条件查询，也不能分页。
**产出**：

- `OrderQueryRequest`（Query DTO）
- `[FromQuery]` 基础绑定
- 在 Service 中用 LINQ 实现：
    - `Where`（过滤）
    - `OrderBy / OrderByDescending`（排序）
    - `Skip / Take`（分页）
- 参数基础保护（如 page/pageSize 边界）

**引出下一章**：功能逐渐完整后，文件数量会增加；需要统一项目结构与命名，避免代码变乱。

------

## 第8部分：项目结构与可维护性

### 第14章 项目结构与命名约定（让代码不乱）

**要解决的问题**：Controller / Service / Repository / DTO / Domain 文件越来越多，不统一会很快混乱。
**产出**：

- 推荐目录结构（基础版）：
    - `Controllers/`
    - `Services/`
    - `Repositories/`
    - `Dtos/Requests`
    - `Dtos/Responses`
    - `Domain/`（`Order`、异常等）
- 命名约定（基础版）：
    - Controller：`OrdersController`
    - Service：`OrderService`
    - Repository：`IOrderRepository` / `InMemoryOrderRepository`
    - DTO：`...Request` / `...Response`
    - 异步方法：`...Async`
- 强化“逻辑分层 + 物理分层”的一致性

**引出下一阶段**：这里“基础版 Controller Web API”的完整功能；下一阶段进入数据库（EF Core）、全局异常处理、日志、配置、认证等适度进阶内容。


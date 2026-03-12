# WEB API (Controller) 进阶

## 1. 从内存到数据库

### 1. 解决什么问题？

第一阶段的 `Order API` 已经完成了分层结构（Controller / Service / Repository），接口也能正常工作。

但数据存放在内存里，程序一重启就丢失。但是我们有新的需求：

- 订单数据要持久化
- 程序重启后数据不能丢
- 项目要更接近真实开发

因此，需要解决从内存切到数据库的配置问题。

### 2. 连接数据库

使用SQL Server数据库，配合Docker容器镜像使用

启动 SQL Server（Docker）

```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -e "MSSQL_PID=Developer" \
  -p 1433:1433 \
  --name webapi-sqlserver \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

检查容器是否运行：

```bash
docker ps
```

如果启动失败，查看日志：

```bash
docker logs webapi-sqlserver
```

到这里，数据库环境已经准备好。

### 3. 使用数据库

我们之前的版本，使用`InMemoryOrderRepository`来存取数据（通过内存）。

现在创建一个用数据库的仓库`SqlServerOrderRepository`来实现同样的功能。

~~不要看懂代码细节~~

```csharp
using Microsoft.Data.SqlClient;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Repositories;

public class SqlServerOrderRepository : IOrderRepository
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=orderDB;User Id=sa;Password=Mxxxxx12345!;TrustServerCertificate=True;Encrypt=False";

    // 简单进程内保护，避免同一次应用运行中重复初始化
    private static bool _initialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        var orders = new List<Order>();

        const string sql = """
                           SELECT Id, Amount, Status
                           FROM Orders
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            orders.Add(MapOrder(reader));
        }

        return orders;
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        const string sql = """
                           SELECT Id, Amount, Status
                           FROM Orders
                           WHERE Id = @id
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapOrder(reader);
    }

    public async Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        // 保持和 InMemory 版本行为一致：新订单状态 = Created
        // 假设 Id 是 IDENTITY
        const string sql = """
                           INSERT INTO Orders (Amount, Status)
                           OUTPUT INSERTED.Id, INSERTED.Amount, INSERTED.Status
                           VALUES (@amount, @status)
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@status", "Created");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Failed to create order.");

        return MapOrder(reader);
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken);

        // 保持和 InMemory 版本行为一致：找不到不抛异常
        const string sql = """
                           UPDATE Orders
                           SET Status = @status
                           WHERE Id = @id
                           """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Order MapOrder(SqlDataReader reader)
    {
        return new Order
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Status = reader.GetString(reader.GetOrdinal("Status"))
        };
    }

    /// <summary>
    /// 确保 Orders 表存在，并在空表时插入与 InMemory 版本一致的初始数据。
    /// </summary>
    private static async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await InitLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync(cancellationToken);

            // 1) 建表（如果不存在）
            const string createTableSql = """
                                          IF OBJECT_ID('dbo.Orders', 'U') IS NULL
                                          BEGIN
                                              CREATE TABLE dbo.Orders
                                              (
                                                  Id INT IDENTITY(1,1) PRIMARY KEY,
                                                  Amount DECIMAL(18,2) NOT NULL,
                                                  Status NVARCHAR(20) NOT NULL
                                              );
                                          END;
                                          """;

            await using (var createCmd = new SqlCommand(createTableSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2) 如果表为空，插入与 InMemoryOrderRepository 对齐的两条默认数据
            const string seedSql = """
                                   IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
                                   BEGIN
                                       INSERT INTO dbo.Orders (Amount, Status)
                                       VALUES (100, N'Created'),
                                              (200, N'Paid');
                                   END;
                                   """;

            await using (var seedCmd = new SqlCommand(seedSql, conn))
            {
                await seedCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }
}
```

在DI容器中注册

```c#
builder.Services.AddScoped<IOrderRepository,SqlServerOrderRepository > ();
```

这样，数据会持久存储在数据库中。

### 4. 直接连接数据库会遇到什么问题？

这种手动连接方式，很快会出现问题：

- 连接字符串到处写，修改时容易漏
- 打开/关闭连接代码重复
- SQL 细节和业务逻辑混在一起
- 后面查询、更新一多，service代码会迅速膨胀

#### **如何优化？**

可以把和数据库操作相关的逻辑抽离出来，封装成一个专用的类 AppDbContext， 类似这样：

```c#
using Microsoft.Data.SqlClient;

namespace WebAPI_Basics.Data;

public class AppDbContext
{
    private readonly string _connectionString;

    private static bool _initialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    /// <summary>
    /// 确保数据库中的 Orders 表存在，并在空表时插入初始数据。
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await InitLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            await using var conn = CreateConnection();
            await conn.OpenAsync(cancellationToken);

            const string createTableSql = """
                                          IF OBJECT_ID('dbo.Orders', 'U') IS NULL
                                          BEGIN
                                              CREATE TABLE dbo.Orders
                                              (
                                                  Id INT IDENTITY(1,1) PRIMARY KEY,
                                                  Amount DECIMAL(18,2) NOT NULL,
                                                  Status NVARCHAR(20) NOT NULL
                                              );
                                          END;
                                          """;

            await using (var createCmd = new SqlCommand(createTableSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string seedSql = """
                                   IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
                                   BEGIN
                                       INSERT INTO dbo.Orders (Amount, Status)
                                       VALUES (100, N'Created'),
                                              (200, N'Paid');
                                   END;
                                   """;

            await using (var seedCmd = new SqlCommand(seedSql, conn))
            {
                await seedCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }
}
```

如果，需求增多时，需要让他实现大量功能：

- 连接管理
- 命令执行
- 查询结果映射到对象
- 参数化查询
- 更新保存
- 模型和表结构关系管理
- 后续数据库结构变更管理
- ...

我们使用时，直接通过DI容器注入使用。

**但是，如何实现这么复杂的逻辑，能完美工作？？需要自己编写？？？**

理论上可以自己写一个 `AppDbContext`，把连接和执行逻辑封装起来。

但有人已经完成了这个工作，不需要自己造轮子，直接可以**继承使用。**



### 5. 使用第三方库 - EF Core？

EF Core 是 .NET 里常用的 ORM（对象关系映射），可以用 C# 对象和 LINQ 操作关系型数据库，而不用一开始就手写大量 SQL。

- 支持多种数据库提供程序（providers），包括 SQL Server；

- SQL Server Provider 同样适用于 Azure SQL。

**EF Core 提供了一个核心基类：**

- `DbContext`

它本质上就是“数据库访问上下文”的成熟实现。

于是当前项目的做法就自然变成：

- 自定义 `AppDbContext`
- 继承 EF Core 的 `DbContext`
- 在此基础上对当前项目自己的数据库访问定义



### 6. 如何操作？

1. #### 安装 EF Core 相关包

    ```bash
    dotnet add package Microsoft.EntityFrameworkCore.SqlServer
    dotnet add package Microsoft.EntityFrameworkCore.Design
    ```

    这两个包先各自承担一个角色：

    - `SqlServer` 包：让 EF Core 能连接 SQL Server
    - `Design` 包：管理数据库结构

2. #### 配置连接字符串

    `appsettings.json`中集中管理，不写死在业务代码里

    ```c#
    // 添加字段
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=localhost,1433;Database=WebApiBasicsDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
      }
    }
    ```

3. #### 优化 `AppDbContext`（继承 `DbContext`）

    优化文件：`Data/AppDbContext.cs`内容

    ```c#
    using Microsoft.EntityFrameworkCore;
    
    namespace WebAPI_Basics.Data;
    
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    
        // 其他逻辑
    }
    ```
    
    暂时只跑通配置
    
    `AppDbContext : DbContext`
    
    我们定义的数据库上下文类继承 **EF Core** 提供的基类 ` DbContext`, 这样，就能使用它的全部功能
    
4. #### 在 `Program.cs` 注册 `AppDbContext`

    先补 `using`：

    ```csharp
    using Microsoft.EntityFrameworkCore;
    using WebAPI_Basics.Data;
    ```

    然后在服务注册区域加入：

    ```csharp
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    ```

以后需要使用 AppDbContext 时，仍然通过 DI 方式。

### 7. 如何验证？

这一章是“数据库基础设施是否接通”，不是“订单表是否已经创建”。

#### **验证 1：SQL Server 容器运行正常**

```bash
docker ps
```

确认容器 `webapi-sqlserver` 正在运行，并且有 `1433:1433` 端口映射。

#### **验证 2：手动连接演示可以成功**

运行前面的手动连接示例代码，确认能输出连接成功信息。

这一步是为了确认：

- SQL Server 环境可用
- 用户名/密码/端口配置正确

#### **验证 3：项目启动不报 `DbContext` 配置错误**

运行项目，确认因为 `AddDbContext` / `UseSqlServer` 相关配置不会报错。

如果报错，优先检查：

- EF Core 包是否安装成功
- `using Microsoft.EntityFrameworkCore;` 是否遗漏
- `DefaultConnection` 名字是否拼错
- SQL Server 容器是否已启动

#### **验证 4：现有接口行为保持不变**

切换回`InMemoryOrderRepository`，当前主线仍然是内存版。

所以以下接口行为应与第一阶段末尾一致：

- `GET /orders`
- `GET /orders/{id}`
- `POST /orders`
- `POST /orders/{id}/pay`

这说明当前改动只是在“接入数据库基础设施”，没有破坏现有业务主线。

### 8. 本章小结

这一章完成了从内存到数据库的第一步过渡：

- 先证明直接连接数据库是可行的
- 再看到直接连接会带来重复和混乱
- 因此需要统一的数据库访问入口
- 使用 EF Core 的 `DbContext` 作为成熟实现
- 定义 `AppDbContext` 并注册到 DI
- 完成数据库基础设施接入

到这里，项目已经具备了“连接数据库的能力”，但还没有真正把订单模型落到数据库表里。

**新的问题来了？**

现在已经有了数据库入口 `AppDbContext`，但还有一个关键问题没解决：

- `Order` 在代码里只是一个类
- 数据库并不知道它该变成什么表、哪些字段怎么约束

因此需要解决：

如何把 `Order` 这类对象关联数据库模型，并明确它和表结构的对应关系。



## 2. 实体关联数据库模型

### 1. 解决什么问题？

第1章已经完成了两件事：

- 项目有了数据库环境（SQL Server + Docker）
- 项目里有了统一数据库入口 `AppDbContext`，并且已经注册到 DI

但是现在还不能说“订单已经进入数据库”了。原因很简单：

- `Order` 目前只是一个 C# 类
- `AppDbContext` 目前只是一个数据库入口类
- 数据库还不知道 `Order` 应该对应什么表、字段怎么存

这一章要解决的问题就是：

**如何把利用 `Order` 类生成数据库模型（表结构），并明确它们之间的对应关系？**

### 2. AppDbContext 里到底要放什么？

我们已经明确了 `AppDbContext`这个类，将作为操作数据库统一入口。

现在如何编写逻辑代码，来实现 `Order` 类（实体entity) 和数据库模型（表结构）的关系？？

目前它只是一个空类：

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
```

它虽然“能连数据库”，但还没有告诉框架：

- `Order` 对应的表名是什么？
- `Id` 是不是主键？
- `Amount` 金额字段用什么精度？
- `Status` 是否允许为空？长度多大？
- 哪些字段不能为空？

------

### 3. 设置模型映射参数

我们的`AppDbContext`类继承与EF core的基类 `DbContext`。

而基类 `DbContext`里定义了创建数据库模型的方法 `OnModelCreating`。

需要重写这个方法，利用我们的实体模型设置相应的参数。

实体模型Order

```c#
public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

基于实体模型，创建数据库模型

```c#
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 重写OnModelCreating方法
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            // ===== 1) 映射规则（Model / Mapping） =====

            // 映射到数据库表名 Orders
            entity.ToTable("Orders");

            // 主键
            entity.HasKey(x => x.Id);

            // Id 由数据库生成（Identity）
            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // Amount: decimal(18,2), 必填
            entity.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            // Status: nvarchar(20), 必填
            entity.Property(x => x.Status)
                .HasMaxLength(20)
                .IsRequired();

            // ===== 2) 种子数据配置（Seed Data Configuration） =====
            // 注意：这是“配置”，不是立刻插入数据库。
            // 真正插入发生在 migration + database update 执行时。

            entity.HasData(
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
            );
        });
    }
}
```

#### **这段配置在表达什么？**

##### `modelBuilder.Entity<Order>()`

表示：传递 `Order` 这类的实体开始构建在数据库里的规则。

##### `ToTable("Orders")`

表示：`Order` 对应数据库里的哪张表？

##### `HasKey(x => x.Id)`

表示：哪个字段是主键？

##### `HasPrecision(18, 2)`

金额字段通常需要明确精度，这里指定为常见的 `18,2`。

##### `IsRequired().HasMaxLength(20)`

表示：

- 状态 `Status` 能不能空？
- 最长多少？

当前业务状态值（如 `Created`、`Paid`）都比较短，而且业务上不应为空，所以这里明确约束。

**`entity.HasData()`**

添加数据库初始种子数据

### 4. 如何验证？

这一章还没开始创建数据库表，**只是填写了映射的规则**。所以验证重点是：

- 代码里的数据库模型定义是否已经写对
- 项目是否还能正常运行

#### 验证 1：项目编译通过

如果报错，优先检查：

- `using Microsoft.EntityFrameworkCore;` 是否遗漏
- `Order` 命名空间是否正确
- `Amount / Status` 字段名是否和模型一致



#### 验证 2：项目正常启动

项目能启动，说明：

- `AppDbContext` 注册仍然正常
- `OnModelCreating(...)` 中的配置语法正确



#### 验证 3：现有接口行为保持不变

这一章仍然没有切换 Repository 实现，当前主线还是内存版。

所以接口行为应保持不变。

这说明这一章改动范围控制正确：只定义数据库模型规则，不提前改业务主线。



### 5. 本章小结

这一章完成的是“如何利用实例模型，并把关键表结构规则写清楚”：

- `AppDbContext` 不再只是一个“空入口”
- 在 `OnModelCreating(...)` 中明确了表名、主键、金额精度、状态字段约束

到这里，`Order` 和数据库表之间的关系已经在代码里定义好了。

**新的问题来了？**

现在只是把规则写在代码里，数据库里还没有真正创建出 `Orders` 表。

也就是说：

- 模型有了
- 规则有了
- 数据库结构还没落地

因此需要解决：如何把表结构规则真正应用到 SQL Server，并且以后字段变化时还能持续管理？



## 3. Migration 基础

### 1. 解决什么问题？

第2章已经把 `Order` 的表结构规则写进了 `AppDbContext`。

但是：

- 规则在代码里
- 数据库里还没有真正的 `Orders` 表

因此，需要解决的就是：如何把代码里的表结构规则应用到 SQL Server，生成真正的数据库表。



### 2. 为什么现在需要 Migration？

我们可以手动写 SQL 建表。

但这样会很快遇到问题：

- 代码改了，数据库不一定同步改
- 不同环境表结构容易不一致
- 后面很难追踪“表是怎么改过来的”

因此，我们需要：

- **用代码化、可记录、可重复执行的方式管理数据库结构变化**

这就是 Migration 要解决的问题。

可以先把它理解成：

- Migration = 数据库结构变更记录 + 执行这些变更的方式

    

### 3. 如何操作？

这一章只做“结构落地”，不改业务主线。

顺序是：

1. 生成第一条迁移（记录结构变化）

2. 把迁移应用到数据库（真正创建表）

3. 检查数据库结构是否符合第2章规则

    

### 4. 实现步骤

这部分需要再控制台里，以命令行的形式操作

#### 4.1 生成第一条迁移（记录结构变化）

在项目根目录执行：

```bash
dotnet ef migrations add InitialCreate
```

这一步做的事不是直接建表，而是：

- 根据当前代码里的模型定义
- 生成一份“数据库结构应该怎么变化”的记录文件

这里命名为 `InitialCreate`，表示第一版初始结构。

执行成功后，项目里会新增 `Migrations` 文件夹，并出现对应迁移文件。

#### 4.2 应用迁移到数据库（真正创建表）

继续执行：

```bash
dotnet ef database update
```

这一步才会把刚才生成的迁移真正执行到 SQL Server。

执行成功后，数据库会出现：

- `Orders` 表
- `__EFMigrationsHistory`（EF Core 用来记录迁移历史）

这里要分清两步的作用：

- `migrations add`：生成结构变更记录
- `database update`：执行结构变更到数据库



### 5. 如何验证？

这一章的验证重点是：数据库里是否已经按代码规则创建出表结构。

#### 验证 1：项目里是否生成了迁移文件

执行 `dotnet ef migrations add InitialCreate` 后，检查项目目录是否出现 `Migrations` 文件夹，并且包含新生成的迁移文件。

这说明结构变更记录已经生成成功。

#### 验证 2：数据库里是否出现了表

执行 `dotnet ef database update` 后，打开数据库工具查看 `WebApiBasicsDb`，确认是否出现：

- `Orders`
- `__EFMigrationsHistory`

如果 `Orders` 表已出现，说明结构已经落地。

#### 验证 3：`Orders` 表结构是否符合规则

检查 `Orders` 表，确认这些内容正确：

- 表名：`Orders`
- 主键：`Id`
- `Amount` 精度符合设置
- `Status` 为必填且长度受限

这一条是在验证：第2章写的规则已经真正影响数据库结构。

#### 验证 4：现有接口行为保持不变

这一章只做数据库结构落地，没有切换仓储实现。

所以当前接口行为应保持不变。



### 6. 本章小结

这一章完成了数据库阶段的重要一步：

- 用 Migration 记录表结构变化
- 把迁移应用到 SQL Server
- 让数据库里真正出现 `Orders` 表

到这里，项目已经从“代码里定义了结构”推进到“数据库里有了真实表结构”。

**新的问题来了？**

现在数据库里已经有 `Orders` 表了，但项目实际读写订单数据的主线仍然是内存版仓储。

也就是说：

- 表有了
- 接口还没用它

因此，需要解决：如何把 `IOrderRepository` 从内存实现切换到数据库实现？



## 4. EF Core Repository

### 1. 解决什么问题？

第3章已经把 `Orders` 表建出来了。

但现在接口读写订单的主线仍然走内存仓储：

- 数据库有表
- API 还没真正用数据库

这一章要解决：在不改 `OrdersController -> OrderService` 的前提下，把 `IOrderRepository` 从内存实现切换为数据库实现。



### 2. 使用新的Repository 

第一阶段已经把数据库/存储细节放在 Repository 层收口了。现在要换数据库，本质上就是：

- 上层（Controller/Service）不动
- 只替换 `IOrderRepository` 的实现

仓储的核心任务是两类：

- **写入**：创建订单、更新订单
- **读取**：按 id 查订单、列出订单

**如果在仓储里直接写 SQL：**

EF Core 提供的 `Database.ExecuteSqlRawAsync(...)` 方法去执行 `INSERT/UPDATE`。

它能做“写入”，但马上会卡在“读取”：

- `GetById` / `GetAll` 的逻辑需要把结果读出，来并映射成 `Order`
- 而`ExecuteSqlRaw` 不返回实体结果集

想要拿到结果，需要写 `DbCommand`、读 `DataReader`、手动映射等等复杂繁琐的操作。

因此，我们需要一个东西，能更优雅地执行上述的逻辑操作，并能返回操作后的数据结构。



### 3. 仓储的 “Order 的数据入口”

EF Core已经帮我们封装好了一个方法 `Set<T>()` ,

`**Set<T>()**`帮我们完成两件事：

- 把查询翻译成 SQL（不用手写 SQL）
- 把结果映射成 `Order` 对象（不用手写映射）

调用这个方法之后，我们可以得到执行SQL语句后的数据表里数据集。



### 4. 如何操作？

顺序只有四步：

1. 新增数据库仓储 `EfOrderRepository`
2. 用 `AppDbContext` 实现最基本的 `GetAll/GetById/Add/Update`
3. 在 DI 里把 `IOrderRepository` 切换为 `EfOrderRepository`
4. 用现有接口验证：数据来自数据库且可持久化

这一章只完成“切仓储”，不处理查询过滤/排序/分页。



### 5. 实现步骤

#### 5.1 新建 `EfOrderRepository`

新建文件：`Repositories/EfOrderRepository.cs`

```csharp
public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await db.Set<Order>().ToListAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await db.Set<Order>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Order> AddAsync(decimal amount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // EF + SQL Server 会自动生成 Id（Identity）
        var order = new Order
        {
            Amount = amount,
            Status = "Created"
        };

        await db.Set<Order>().AddAsync(order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        // SaveChanges 后 order.Id 会被填上
        return order;
    }

    public async Task UpdateStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = await db.Set<Order>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
            return;

        order.Status = status;
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

**核心方法：**

```csharp
_db.Set<Order>()
```

它就是“拿到 Order 这类数据入口”的方法。

后面的 `ToListAsync / FirstOrDefaultAsync / AddAsync` 都是EF core内置的扩展方法。

#### 5.2 在 DI 中切换仓储实现

在 `Program.cs` 里，把原来内存仓储的注册换成 EF 版：

```csharp
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
```



### 6. 如何验证？

验证目标是两件事：

- 接口行为不变（Controller/Service 没改）
- 数据真的来自数据库（可持久化）

#### 验证 1：创建订单后重启项目，数据还在

1. `POST /orders` 创建订单
2. 停止项目再启动
3. `GET /orders` 还能查到刚才的订单

如果这成立，就说明已经从内存切到数据库。

#### 验证 2：支付后状态能持久化

1. 创建订单（状态 `Created`）
2. 调用 `pay`
3. 再次 `GET /orders/{id}`，看到状态为 `Paid`
4. 重启项目后再查一次，状态仍为 `Paid`

如果这成立，说明更新也真正落在数据库。



### 7. 本章小结

这一章只完成一件事：

- 新增 `EfOrderRepository`
- 用 `AppDbContext` 做最基本的增删改查中的“查/增/改”
- DI 切换 `IOrderRepository` 的实现
- 上层主线保持不变

到这里，API 已经真正开始使用数据库持久化订单数据。

**新的问题来了？**

现在 `GetAll` 是直接把整张 `Orders` 表全部取出来，再由上层去处理过滤/排序/分页。

这在使用内存的Repository时，不影响，反正都是内存在处理。

但使用数据库后，如果数据量一大就不合理。

数据库想不内存，有更强大的能力来处理数据。

因此，需要解决：如何把过滤/排序/分页这些查询动作放到数据库侧执行，而不是先取全量数据再处理。



## 5. 查询下推

### 1. 解决什么问题？

现在 `EfOrderRepository.GetAllAsync` 是直接把整张 `Orders` 表 `ToListAsync()` 拉回内存，然后再由上层Service去做过滤/排序/分页操作。

这样意味着：

- 数据库里有 10 万条订单
- 只想看第一页 20 条
- 但仓储仍然会把 10 万条全取回来，再在内存里 `Skip/Take`

问题不在“能不能跑”，而在“做法不对”：

- 读了大量不需要的数据
- 网络传输、内存、CPU 都浪费
- 数据越多越明显

因此，既然数据在数据库里，就应该让数据库完成过滤/排序/分页，而不是先把全量数据拉回内存再处理。



### 2. 仓储要怎么“表达查询”？

仓储里需要有一个对象，它具有“可继续拼条件”的查询能力IQueryable。

大部分常见的集合，比如`List<T>`类型的对象，都有`IQueryable` , 即能链式调用LINQ扩展方法，但这些都是在操作内存中的数据。

而我们需要一个新的类型，具备`IQueryable`， 并且能操作数据库里的数量。

**EF core提供了这个类型`DbSet<T>`**：

- 是 调用`Set<>()`方法的返回值类型
- 表示T类型在数据库上数据表数据记录的集合 - 比如，就是数据库上的order的那张表
- 提供 `IQueryable<T>`，让后面的 `Where/OrderBy/Skip/Take` 继续组合
- 提供对数据记录进行修改的能力

也就是说，在数据库上下文中，我们通过`Set<Order>()`方法创建的表格（类型就是`DbSet<Order>`), 这个表格对象就可以先执行LINQ操作（EFcore会转化为实际的SQL语句操作），最后转换为List， 返回到内存中，给上层的service使用。



### 4. 如何操作？

1. 数据库上下文对象中，申明Order数据表格对象

1. 把“查询条件”传进仓储（让仓储知道要怎么查）

2. 在仓储里从 `DbSet<Order>` 开始拼查询，再 `ToListAsync()`

3. 上层不再对全量结果做过滤/排序/分页

    

### 5. 实现步骤

#### 5.1 在 `AppDbContext` 里申明 `DbSet<Order>`表格对象

`Data/AppDbContext.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 映射配置保持不变
        base.OnModelCreating(modelBuilder);
        // ...
    }
}
```

#### 5.2 让仓储接收查询条件

修改接口`IOrderRepository`：

能接收 `OrderQueryRequest query`

```csharp
Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken);
```

#### 5.3 在 `EfOrderRepository` 里拼查询并下推

核心点：**从 `db.Orders` 开始，先得到 `IQueryable<Order>`，再按条件逐步拼，最后 `ToListAsync()`。**

`Repositories/EfOrderRepository.cs`：

```csharp
public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 拿到数据表数据集合对象
        DbSet<Order> orders = db.Orders;

        // 执行查询过滤的操作
        // 定义一个变量来保存链式查询的结果
        IQueryable<Order> result = orders; // 初始化一下，防止为空
        // 1) 过滤
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            result = result.Where(o => o.Status == status); 
        }

        // 2) 排序
        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var sortDir = query.SortDir?.Trim().ToLowerInvariant();
        var desc = sortDir == "desc";

        result = sortBy switch
        {
            "amount" => desc ? result.OrderByDescending(x => x.Amount) : result.OrderBy(x => x.Amount),
            "id"     => desc ? result.OrderByDescending(x => x.Id)     : result.OrderBy(x => x.Id),
            _        => result.OrderBy(x => x.Id)
        };

        // 3) 分页
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = Math.Min(pageSize, 100);

        result = result.Skip((page - 1) * pageSize).Take(pageSize);
		
        // 查询的结果转List
        return await result.ToListAsync(cancellationToken);

    }

   // 其他方法
}
```

**注意**：

- 数据库原表格对象 `DbSet<Order> orders = db.Orders;`不能改变，因为是查询
- `IQueryable`调用链式LINQ操作的对象可以变，初始值赋值于`DbSet<Order> orders = db.Orders`
- 之后所以的查询结果都基于新的变量`result`, 而不是数据库原表格对象`orders`
- 最后被转换成List的对象，也是那个新的变量

到这里，下推就完成了：Where/OrderBy/Skip/Take 都会进 SQL，数据库只返回“需要的那一页”。

#### 5.4 修改service层

```c#
public class OrderService(IOrderRepository repo)
{
    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken)=>await repo.GetAllAsync(query,cancellationToken);

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



### 6. 如何验证？

验证目标：**同样的接口功能，但不再全表读取。**

#### 验证 1：分页行为正确

- 先插入多条订单（比如 30 条）
- 请求 `Page=1, PageSize=10` 返回 10 条
- 请求 `Page=2, PageSize=10` 返回另一批 10 条
- 请求 `Page=3, PageSize=10` 返回最后 10 条

#### 验证 2：过滤/排序行为正确

- `Status=Paid` 只返回 Paid 的订单

- `SortBy=amount&SortDir=desc` 金额从大到小

    

### 7. 本章小结

这一章做的事很单一：

- 过去：仓储全表 `ToListAsync()`，上层在内存处理查询
- 现在：仓储从 `DbSet<Order>`（`db.Orders`）开始拼查询，把过滤/排序/分页下推到数据库，然后才执行 `ToListAsync()`

**新的问题来了？**

现在查询已经更像数据库该有的样子了，但更新逻辑还比较“靠直觉”：

- 为什么有时改了属性 `SaveChanges` 就会更新？
- 为什么有时又不会？
- 什么时候需要先查出来？什么时候可以更直接更新？



下面开始按你现在“进阶笔记”的推进逻辑来写**第6章**：它要自然承接第5章「查询下推」之后的“新问题”——**更新到底是怎么发生的？为什么有时改了属性就能更新，有时又不更新？什么时候必须先查？什么时候可以直接更新？**



## 6. EF Core 更新机制

### 1. 解决什么问题？

在第4~5章的 EF Repository 里，更新状态是这么写的（逻辑类似）：

- 先 `FirstOrDefaultAsync` 把订单查出来
- `order.Status = "Paid"`
- `db.SaveChangesAsync()`

为什么“改属性 + SaveChanges”就能更新？

有没有办法不先查出来就更新？

因此需要弄清楚 EF Core 的“更新底层规则”。



### 2. EF Core 为什么知道改了什么？

#### 2.1 DbContext 会“追踪”实体（Tracking）

当用下面这种方式查数据：

```csharp
var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
```

EF Core 默认会做一件事：

- 把 `order` 放进 DbContext 的“跟踪器（Change Tracker）”
- 记录它当前的属性值快照（原始值）

随后执行如下操作的话：

```csharp
order.Status = "Paid";
```

EF Core 会发现：

- `Status` 从原值变成了新值
- 这个实体进入“Modified”状态 （被修改）

最后调用：

```csharp
await db.SaveChangesAsync(ct);
```

EF Core 就会生成类似这样的 SQL（概念）：

```sql
UPDATE Orders SET Status = N'Paid' WHERE Id = @id;
```

**更新能发生，是因为实体被 DbContext 跟踪了。**



### 3. 为什么有时候改了属性却“不更新”？

#### 3.1 不让EF core跟踪查询 

调用 `AsNoTracking()`方法，可以不让EF core跟踪。

如果写了：

```csharp
var order = await db.Orders
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.Id == id, ct);
```

此时 `order` **不在 Change Tracker 里**。

改它的属性，DbContext 根本不知道——`SaveChanges` 也就不会生成 UPDATE。

适用场景：**纯查询（GetAll/GetById）**，为了更快、更省内存。

#### 3.2 你拿到的是“新对象”，不是 DbContext 跟踪的那个对象

比如自己 new 了一个：

```csharp
var order = new Order { Id = id, Status = "Paid" };
order.Status = "Paid";
await db.SaveChangesAsync(ct);
```

这也不会更新，因为 DbContext 从来没追踪过它。



### 4. 两种更新方式

现在的 `Pay` 更新，是“典型业务写法”——**先查再改**。但这不是唯一方式。

#### 写法 A：先查出来再更新（最稳、最常见）

代码如下:

```csharp
public async Task UpdateStatusAsync(int id, string status, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct); // 默认跟踪
    if (order is null) return;
    order.Status = status;
    await db.SaveChangesAsync(ct);
}
```

典型使用场景：

如果需要如下操作时：

- 读取当前状态后做判断（比如：只有 Created 才能变 Paid）
- 要触发领域逻辑（比如以后有更多字段要改、要计算）
- 想要拿到“更新后的实体”（比如更新后返回给上层）

因此当前的业务需要跟踪：`Pay` 需要判断是否重复支付。

#### 写法 B：不查，直接更新（更高效，但要懂边界）

当只想做“把某字段改成某值”，而且**不需要把整行数据先读出来**，就可以直接更新。

EF Core 7+ 提供了非常适合“直接更新”的方法：`ExecuteUpdateAsync`（它会直接生成 UPDATE SQL）。

实例代码：直接 UPDATE

```csharp
public async Task<bool> UpdateStatusDirectAsync(int id, string status, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    var affected = await db.Orders
        .Where(x => x.Id == id)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(o => o.Status, status), ct);

    return affected > 0;
}
```

直接更新这有什么好处？

- **不会先 SELECT**
- 数据量大/并发高时更省资源
- 非常适合：批量更新、简单字段更新

典型使用场景：

当需要：像“后台批量把某些订单状态改成 X”这种场景



### 5. 代码实例

Pay 的两种实现对比

#### 版本 1（当前的版本）

Service 里：

- `GetByIdAsync` 查
- 判断状态
- `UpdateStatusAsync` 改 + SaveChanges

这符合前面“业务规则与数据访问分离”的结构（Service 做判断，Repo 做存取）。

#### 版本 2（Repo 直接更新 + 返回影响行数）

如果想让 Repo 更高效，可以让 Repo 暴露一个“直接更新”的方法，返回 bool：

```csharp
Task<bool> UpdateStatusDirectAsync(int id, string status, CancellationToken ct);
```

Repo 内部用 `ExecuteUpdateAsync`，Service 仍负责业务判断。

但注意：Service 若需要判断“已支付”，仍要先查一次——这条 SELECT 是业务需要，省不了。



### 6. 如何验证

验证的目标不是功能，而是**确认理解EF 更新规则**：

#### 验证 1：加上 `AsNoTracking()` 后，改属性不再更新

- 在 Repo 的 `GetByIdAsync` 故意加 `AsNoTracking()`
- 然后用“先查再改”的方式更新
- 你会发现 `SaveChanges` 不会产生 UPDATE（或者更新无效）

#### 验证 2：直接更新方法不会触发 SELECT

- 用 `ExecuteUpdateAsync`
- 打开 EF Core SQL 日志（它只会发 UPDATE）



### 7. 本章小结

1. **为什么改属性 + SaveChanges 能更新？**

    因为实体被 DbContext 跟踪，Change Tracker 发现属性变了。

2. **为什么有时不更新？**

    常见是 `AsNoTracking()` 或者对象从来没被 DbContext 追踪。

3. **什么时候必须先查？什么时候可以直接更新？**

    - 需要业务判断/需要实体数据：先查再改
    - 只是简单字段更新/批量更新：直接 UPDATE 更高效

**新的问题？**

现在可以“查得对（下推）”和“改得对（更新机制）”了。

但是问题是：

当客户端发送http请求时，如果请求异常，当前我们在controller里的每个action里通过catch捕获的。但时随着Action的增多，这样的处理方式不合理。

因此需要解决：查询结果异常的全局处理。



## 7. middleWare - 全局异常处理

### 1. 解决什么问题？

当前的API， Service 抛业务异常，Controller catch 后映射成 HTTP 404/409。

这在少量接口时没问题，但当接口变多，会出现这种重复结构：

- 每个 Action 都写一段 try/catch
- 每段里面都是“把业务异常翻译成状态码”的同一套逻辑

这就导致如下问题：

- **重复**：每个 Action 复制粘贴，维护成本高
- **不一致**：今天忘了写一个 catch，就可能变成 500
- **Controller 变厚**：HTTP 输入输出之外塞满错误映射代码

因此，需要解决的是：

**让 Controller 不再写 try/catch，而是把“异常 → HTTP 响应”的翻译放到一个统一的位置。**



### 2. 一次请求的真实流程

要把 try/catch 从每个 Action 移走，就必须回答一个问题：

- **如果 Controller 不 catch，那异常谁来 catch？**

先要明确一下对于一次请求来说：

- 请求是如何到Controller的？
- 到Controller层之后又会去哪里？
- 它是真实流程是什么？

#### 1.  请求是怎么来的？

- Scalar 在浏览器里通过 URL 发请求（例如 `https://localhost:5001/...`）
- 这个 URL 对应本机上一个正在监听的服务端：**Kestrel**

#### 2. Kestrel 做了什么？

Kestrel 是框架自带的默认的 Web API 宿主服务器，它负责：

- 监听 host:port
- 接收 HTTP 请求
- **为每次请求创建/准备一个 `HttpContext`**（这次请求的“上下文容器”）
- 把 `HttpContext` 交给 ASP.NET Core 的处理流水线
- 最后把响应发回给浏览器（Scalar）

#### 3. 请求管道（Pipeline）是什么？

Kestrel 把 `HttpContext` 交给 ASP.NET Core 之后，不是立刻进 Controller，而是先走：

**一串中间件（Middleware）组成的管道**

可以把中间件理解成很多个“同样形状的函数”，每个都长这样：

```text
Middleware(context):
  1) 做点前置工作
  2) await next(context)  // 把请求交给下一环（最终到 Controller）
  3) 做点后置工作
```

关键句就是：`await next(context)`

它让中间件成为“洋葱模型”——外层包着内层。

------

#### 4. 中间件为什么能替代 Controller 的 try/catch？

现在看到了“Controller 上面是谁”：

- Controller 是管道深处的某个终点（Endpoint）
- 它外面包着一层又一层中间件

所以如果我们想把 try/catch 从每个 Action 收口到一个地方，最佳位置就是：

> **在管道里靠前的某个中间件里，用 try/catch 包住 `await next(context)`**

为什么这样就能“全局”？

因为 `next(context)` 代表“后面所有步骤”：

- 后续中间件
- 路由匹配
- Controller Action
- Action 里调用的 Service/Repo/EF

于是：

- Service 抛异常
- 沿调用链冒泡到 Controller（Controller 不 catch）
- 再冒泡回到外层中间件的 try/catch
- 被统一处理

这就实现了：**Controller 的 try/catch 消失，但异常仍然能接住**。

------

#### 5. catch 到异常后，怎么返回 HTTP 响应？

**HTTP 响应是只在Controller 层吗？**

按管道模型：

- 响应最终是通过 `HttpContext.Response` 写出的
- **中间件也拿到了 HttpContext**
- 所以中间件同样可以设置：
    - `StatusCode = 404/409`
    - 写入 body（简单文本即可）
- 请求结束后，Kestrel 会把 `HttpContext.Response` 发送回浏览器

所以：Controller 只是“写响应的一种方式”，不是唯一方式。

“真正发送”由 Kestrel 完成，使用时我们只是在Controller/中间件里只是把 Response 填好。



### 3. 异常生命周期

以“支付不存在订单”为例，抛出一个异常的全流程：

1. Scalar（浏览器）访问 URL → 发送请求
2. Kestrel 接到请求 → 创建/准备 HttpContext
3. 请求进入管道 → 先到全局异常处理中间件（外层）
4. 中间件执行 `await next(context)` → 请求继续深入，到 Controller → Service
5. Service 查不到订单 → throw `OrderNotFoundException`
6. 异常冒泡：Service → Controller（没 catch）→ 回到中间件 catch
7. 中间件写 `HttpContext.Response.StatusCode = 404` 并写 body
8. 管道结束 → Kestrel 把 Response 发送回 Scalar
9. Scalar 显示 404



### 4. 如何操作

#### 1. 创建全局异常处理中间件

核心结构只有一句话：

> **try { await next(context); } catch { 写 Response }**

```csharp
public sealed class GlobalExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // 关键：next(context) 代表后续全部流程（路由、Controller、Service、Repo/EF）
            await next(context);
        }
        catch (OrderNotFoundException ex)
        {
            await WritePlainErrorAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (OrderConflictException ex)
        {
            await WritePlainErrorAsync(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (Exception)
        {
            //最小可用兜底异常
            await WritePlainErrorAsync(context, HttpStatusCode.InternalServerError, "Unexpected error.");
        }
    }

    private static async Task WritePlainErrorAsync(HttpContext context, HttpStatusCode status, string message)
    {
        // 如果响应已经开始写（header/body 已部分输出），就不要再强行改状态码
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(message);
    }
}
```

**几点说明：**

- `HttpContext`：Kestrel 每个请求创建/准备一个，贯穿整个管道
- `await next(context)`：把请求交给后续处理并等待返回
- `try/catch 包住 next`：就能“全局”接住后续任何 throw
- 中间件写 `context.Response`：响应不是 Controller 专属；最终由 Kestrel 发送回客户端

#### 2. 接入管道：使用中间件

**注意放置顺序。**

把它注册在 `MapControllers()` 之前，这样它才能“罩住” Controller。

```c#

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
        ...
        
        // 全局异常处理中间件：放在 MapControllers 之前，才能罩住 Controller/Service
        app.UseMiddleware<GlobalExceptionMiddleware>();

        app.MapControllers();
        app.Run();
    }
}
```

#### 3. 修改Controller ：删除 try/catch

```c#
[ApiController]
[Route("[controller]")]
public class OrdersController(OrderService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderResponse>>> GetAll([FromQuery] OrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        var result = (await service.GetAllAsync(query, cancellationToken)).Select(ToResponse).ToList();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var order = await service.GetByIdAsync(id, cancellationToken);
        return Ok(ToResponse(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(OrderCreateRequest request,
        CancellationToken cancellationToken)
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
        var order = await service.PayAsync(id, cancellationToken);
        return Ok(ToResponse(order));
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

Controller 现在只写“正常路径”：

- 调用 service
- 返回 Ok/Created
- 不再负责翻译异常

**注意：要求 Service 继续用 `throw` 表达业务失败，否则就没有“全局异常映射”的输入。**



### 5. 如何验证

#### 验证1：支付不存在订单 → 404

- 调 `POST /orders/999999/pay`
- Service 抛 `OrderNotFoundException`
- Controller 不 catch → 异常冒泡
- 中间件 catch → 写 `StatusCode=404`
- Scalar 收到 404

#### 验证2：重复支付 → 409

- 同订单支付两次
- 第二次 Service 抛 `OrderConflictException`
- 中间件映射为 409

#### 验证3：Controller 里 “await 后面的代码不会执行”

Controller `await` 后加一行（断点/日志），发生异常时不会执行到，证明异常不是返回值，而是控制流跳转到外层 catch。



### 6. 本章小结

现在API已经完成：

1. 使用中间件对**异常翻译集中管理**：404/409/500 规则统一
2. **Controller 变薄**：只写 HTTP + 调用

**有什么新的问题？**

现在状态码统一了，但会发现：

- 业务异常：中间件返回的是 plain text
- 参数验证错误（[ApiController] + DataAnnotations）：默认返回另一种 JSON 结构
- 未知异常：又是第三种风格

这会导致客户端处理很麻烦。

因此还需要解决：**统一错误响应结构（ProblemDetails）**，让“错误结构稳定”跟“状态码正确”一样成为默认。



你这个质疑是合理的：我上一版第8章里突然出现 `using Microsoft.AspNetCore.Mvc;`，**如果不解释清楚来源和必要性，看起来就像“随手抄来的代码”**。

但这里我得把事实说清楚：
`ProblemDetails` / `ValidationProblemDetails` **这两个类型本来就定义在 `Microsoft.AspNetCore.Mvc` 命名空间里**（Controller 项目本来就引用了 MVC）。所以：

- 你想在中间件里 new 一个 `ProblemDetails` → **就必须 `using Microsoft.AspNetCore.Mvc;`**
- 这不是“瞎抄”，而是因为类型就在这个 namespace 里
- 反过来：如果你不想引入这个 using，也可以不用框架的 `ProblemDetails` 类型，自己定义一个 `ProblemDetailsDto`（但那样就不叫“ProblemDetails 统一规范”，也失去跟框架默认验证错误格式对齐的价值）

下面我按你的要求来：**按你笔记的逻辑与格式（问题→机制→最小改动→验证→小结→引出）**，重新生成**第8章**。并且我会明确写清楚“为什么需要这个 using”，不让它看起来像突然冒出来的东西。



## 8. 统一错误响应：ProblemDetails

### 1. 解决什么问题？

上面我们完成了“全局异常处理”的收口：

Controller 不再写重复 try/catch，Service 继续用 `throw` 表达业务失败，由中间件统一映射状态码（404/409/500）。

但现在又出现一个新问题：

- **业务异常**（中间件返回）：可能是 `text/plain`
- **验证错误**（`[ApiController]` + DataAnnotations）：框架默认返回一套 JSON
- **未知异常**：又可能是另一种结构/文本

结果：前端/调用方要写很多兼容逻辑，错误处理非常难统一。

因此，我们期望：

- **不管错误来自哪里，都返回同一种结构：ProblemDetails**



### 2. ProblemDetails 是什么？

`ProblemDetails` 是一种标准的 HTTP API 错误响应格式，定义在 **RFC 7807** 中。

简单来说，它是为了解决一个常见的 API 开发痛点：

- **不同系统的错误返回格式五花八门，导致客户端解析异常非常麻烦。**

#### 为什么需要它？

在没有标准之前，如果 API 出错，有的开发者返回 `{ "message": "错误信息" }`，有的返回 `{ "error_code": 1001, "reason": "..." }`，还有的直接返回 HTML 错误页面。这导致前端或其它消费 API 的服务必须为每一个后端写一套专门的错误解析逻辑。

`ProblemDetails` 提供了一个“统一语言”，让所有 API 以相同的结构来描述问题。

#### 结构长什么样？

它本质上是一个 JSON 对象，包含以下核心字段：

- **`type`** (可选): 一个 URI，指向描述该错误的文档（比如官方文档链接）。
- **`title`** (可选): 错误的简短描述（例如 "Bad Request"）。
- **`status`** (可选): HTTP 状态码（例如 400）。
- **`detail`** (可选): 具体的错误解释（例如 "用户名格式不正确"）。
- **`instance`** (可选): 发生错误的具体请求 URI。

#### 示例：

JSON

```
{
  "type": "https://example.com/probs/out-of-credit",
  "title": "余额不足",
  "status": 403,
  "detail": "您的账户余额不足以完成此购买。",
  "instance": "/account/12345/buy"
}
```



### 3. 如何操作

对于现在的**验证错误**，在（`[ApiController]` + DataAnnotations）模式下：

- DTO 验证失败时（ModelState invalid）
- **Action 不会执行**
- 框架默认直接返回 **ValidationProblemDetails**（也是 ProblemDetails 家族）

因此，为了统一返回的格式，并且对齐框架的默认标准，需要把异常处理中间件的返回格式也定义成ProblemDetails 风格。



### 4. 代码实现

#### 1）修改中间件：WritePlainErrorAsync → WriteProblemAsync

当前中间件现在写的是 `text/plain`。

只改“写响应”这一段，让它输出 ProblemDetails JSON。

**修改文件：`WebAPI_Basics/Middlewares/GlobalExceptionMiddleware.cs`**

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc; // ProblemDetails 在这里

namespace WebAPI_Basics.Middlewares;

public sealed class GlobalExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OrderNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (OrderConflictException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (Exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "Unexpected error.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
```

#### 2）注册方式（方式不变）

`Program.cs` 保持原来的注册位置：

```csharp
app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();
```



### 5. 如何验证

#### 验证1：业务异常现在也是 ProblemDetails（404/409）

- `POST /orders/999999/pay`
    预期：404 + `application/problem+json`，body 有 `status/title/detail/instance`

#### 验证2：验证错误仍是默认 ValidationProblemDetails

- 对 Create 接口传一个不合法 DTO（触发 DataAnnotations）
    预期：400 + `application/problem+json`，并且 body 里有 `errors` 字段（ValidationProblemDetails）



### 6. 新方式

新的项目更多使用框架集成的中间件和接口，AddProblemDetails + UseExceptionHandler + IExceptionHandler

逻辑不变：仍然要映射 404/409/500，只是把“异常处理入口”换成框架推荐方式。

#### 新方式要解决的核心点

- 旧方式：自己写一个 Middleware 并手动写 Response
- 新方式：让框架的异常处理管线接管异常，并用 ProblemDetails 服务输出

关键组件：

- `builder.Services.AddProblemDetails()`：注册 ProblemDetails 能力
- `app.UseExceptionHandler()`：启用全局异常处理入口
- 自定义 `IExceptionHandler`：决定不同异常映射成什么 status/title/detail（业务规则仍在这里）

#### 1）Program.cs：注册 ProblemDetails + ExceptionHandler

在 `builder.Services.AddControllers()` 附近添加：

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<OrderExceptionHandler>();
```

然后在 `app` 管道中（**放在 MapControllers 之前**）：

```csharp
app.UseExceptionHandler();
app.MapControllers();
```

> 注意：如果要测试新方式，**先把旧的 `UseMiddleware<GlobalExceptionMiddleware>()` 注释掉**，否则两套同时存在不好判断是谁在处理。

#### 2）新增异常处理器：OrderExceptionHandler

**新建文件：`WebAPI_Basics/Middlewares/OrderExceptionHandler.cs`**

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI_Basics.Middlewares;

public sealed class OrderExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public OrderExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // 这里做“异常 → 状态码”的映射（跟第7章一样，只是换了入口）
        int status;
        string title;
        string detail;

        switch (exception)
        {
            case OrderNotFoundException:
                status = StatusCodes.Status404NotFound;
                title = "Not Found";
                detail = exception.Message;
                break;

            case OrderConflictException:
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = exception.Message;
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                title = "Internal Server Error";
                detail = "Unexpected error.";
                break;
        }

        httpContext.Response.StatusCode = status;

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        };

        // 让框架按 ProblemDetails 标准输出（content-type/序列化等交给框架）
        await _problemDetailsService.WriteAsync(problemContext);

        return true; // 表示异常已经处理完了
    }
}
```

注意：

- `UseExceptionHandler()` 会在异常冒泡到“全局入口”时触发处理流程
- `IExceptionHandler` 就是框架提供的“异常映射钩子”
- `IProblemDetailsService` 负责把 `ProblemDetails` 按标准写回去

#### 验证方式

跟上面方式一样：

- `POST /orders/999999/pay` → 404 + problem+json
- 重复支付 → 409 + problem+json
- 验证错误 → 默认 ValidationProblemDetails（仍然是 problem+json）

并且应该能观察到：即使不再使用自写的全局异常中间件，效果仍一致。



### 7. 本章小结

本章做了两件“让 API 更成熟”的事： 

1. **旧方式**：在现有 GlobalExceptionMiddleware 上，最小改动输出 ProblemDetails
2. **新方式**：用 AddProblemDetails + UseExceptionHandler + IExceptionHandler 实现同等效果

现在“业务异常 + 验证错误”都统一成 `application/problem+json`，客户端可预测。

**有什么新的问题？**

现在错误返回虽然统一，但当线上报错时，仍需要服务端记录关键流程与异常细节，才能定位问题。

因此需要解决如何记录常信息的问题。



## 9. 日志（ILogger）

### 1. 解决什么问题？

第8章我们已经解决了“错误响应结构不统一”的问题：

- 业务异常返回 `ProblemDetails`
- 验证错误默认返回 `ValidationProblemDetails`
- 客户端拿到错误时，可以看到稳定的错误结构

但是现在马上会出现一个新问题：

**客户端虽然看到错误了，但服务端不好查**

比如用户说：

- “我刚刚支付失败了”
- 错误响应里有一个 `traceId`

这时后端真正关心的是：

- 这次请求到底走到哪一步了？
- 是在哪一层失败的？Controller、Service、Repository，还是数据库？
- 如果是业务失败，到底是“订单不存在”，还是“重复支付”？
- 如果是 500，具体异常是什么？

也就是说：

第8章解决的是“客户端看见什么错误”，但现在需要解决的是**“服务端怎么查这个错误”。**



### 2. 如果不加日志，会有什么后果？

对于服务端如何方便拍查错误，通常需要使用日志。

如果没有日志，系统会进入一种“能跑，但不好维护”的状态：

#### 1）只能看到结果，看不到过程

客户端只能看到：

- 404 / 409 / 500
- title / detail / traceId

但是看不到：

- 请求有没有进 Service
- 查询有没有成功
- 更新有没有执行
- 异常到底在什么位置抛出

#### 2）线上问题几乎只能靠猜

开发时可以打断点。但部署后，用户报错时不可能总是 attach 调试器。

如果没有日志，通常只能问：

- 几点报错的？
- 哪个接口？
- 能不能再复现一次？

#### 3）第8章的 traceId 也发挥不了作用

第8章里错误响应中的 `traceId`，本质上是给客户端的一把“钥匙”。

但如果服务端日志里没有同一个标识，这把钥匙就开不了门。

所以我们的目标非常明确：

> **让服务端日志记录关键流程，并且能和第8章返回给客户端的 traceId 对上。**



### 3. 什么是 ILogger？

可以直接使用ASP.NET Core 内置的日志系统。

#### 1）ILogger 通过 DI 注入

和前面的 Repository / Service 一样，日志对象也可以直接注入：

```csharp
ILogger<OrderService> logger
```

#### 2）最常用的三个日志级别

- `LogInformation`：正常关键流程
- `LogWarning`：可预期的业务失败
- `LogError`：非预期异常

#### 3）日志记录的是“过程”，不是“业务结果”

业务结果仍然是：

- 正常返回数据
- 或者抛异常 → 第7/8章统一处理

日志只是把过程记下来，方便排查。



### 4. 手动把串联traceId 和日志

为了更好理解整异常响应（给客户端的）和日志系统（给服务端的）的关联，手动串联时：

- 异常响应还是使用自定义的全局中间件 `GlobalExceptionMiddleware`
- 日志模块使用框架内置的

#### 4.1 先明确做什么

目标只有两个：

1. **记录 Pay 的关键流程日志**
2. **让日志里显式带上和客户端响应相同的 traceId**

也就是说，客户端报错后并传递一个 `traceId`，在服务端日志里搜同一个 `traceId`，就能找到对应的请求过程。

#### 4.2  traceId 从哪里来？

每次请求都会有一个 `HttpContext`。

而 `HttpContext` 上有一个很重要的属性：

```csharp
context.TraceIdentifier
```

它表示“这次请求的标识”。

所以获取traceId最直观的做法就是：

- 返回给客户端的错误响应里放这个值
- 日志里也显式记录这个值

这样客户端和服务端就能通过同一个 id 连接起来。

#### 4.3 如何让 Service 层也拿到 traceId？

给客户端的错误响应是在中间件层，而记录日志的逻辑是在业务层（service层）。

我们前面讲过：

- `HttpContext` 是“每次请求一个”的上下文对象
- 中间件里能直接拿到它
- Controller 里也能间接访问它

但 Service 默认拿不到 `HttpContext`。

如果想在 Service 里也拿到`HttpContext`，并且读取当前请求的 `traceId`，就要通过框架提供的：

```csharp
IHttpContextAccessor
```

它的作用很简单：

> **在非 Controller / 非 Middleware 的地方，访问当前请求的 HttpContext。**

#### 4.4 异常中间件添加traceId

给异常响应自定义的全局中间件 `GlobalExceptionMiddleware` 添加traceId字段

```c#
public sealed class GlobalExceptionMiddleware(RequestDelegate next)
{
   ...

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail
        )
    {
        ...
        // 添加traceId字段
        problem.Extensions.Add("traceId", context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(problem);
    }
}
```

#### 4.4 注册 IHttpContextAccessor

**Program.cs**

```csharp
builder.Services.AddHttpContextAccessor();
```

这一步只做一件事：

让后面 Service 能通过 DI 拿到 `IHttpContextAccessor`。

#### 4.5 在 OrderService 中注入 ILogger 和 IHttpContextAccessor

**Services/OrderService.cs**

```csharp
using Microsoft.Extensions.Logging;
using WebAPI_Basics.Domain;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(
    IOrderRepository repo,
    ILogger<OrderService> logger,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken cancellationToken) =>
        await repo.GetAllAsync(query, cancellationToken);

    public async Task<Order> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await repo.GetByIdAsync(id, cancellationToken) ?? throw new OrderNotFoundException(id);

    public async Task<Order> CreateAsync(OrderCreateRequest request, CancellationToken cancellationToken) =>
        await repo.AddAsync(request.Amount, cancellationToken);

    public async Task<Order> PayAsync(int id, CancellationToken cancellationToken)
    {
        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier;

        logger.LogInformation("Pay started. OrderId={OrderId}, TraceId={TraceId}", id, traceId);

        var order = await repo.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Pay failed: order not found. OrderId={OrderId}, TraceId={TraceId}", id, traceId);
            throw new OrderNotFoundException(id);
        }

        if (order.Status == "Paid")
        {
            logger.LogWarning("Pay failed: order already paid. OrderId={OrderId}, TraceId={TraceId}", id, traceId);
            throw new OrderConflictException($"Order {id} was already paid");
        }

        await repo.UpdateStatusAsync(id, "Paid", cancellationToken);

        logger.LogInformation("Pay succeeded. OrderId={OrderId}, TraceId={TraceId}", id, traceId);

        return order;
    }
}
```

#### 4.6 原理是什么？

整条链路：

1. 客户端发请求
2. Kestrel 创建 `HttpContext`
3. 这次请求有自己的 `TraceIdentifier`
4. Service 通过 `IHttpContextAccessor` 取到当前请求的 `TraceIdentifier`
5. 日志里显式把这个 `TraceId` 打出来
6. 第8章错误响应里也带这个 `traceId`
7. 客户端给后端一个 `traceId`
8. 后端在日志里搜同一个 `traceId`，就能找到这次请求的流程

这就是“traceId 和日志真正连起来”的机制。

#### 4.7 对未知异常（500）处理

因为 `PayAsync` 里记录的是“业务过程日志”，但对未知异常（500）来说，最关键的信息通常还是异常对象本身。

因此对于对未知异常（500）的日志记录，需要再中间件中添加：

**Middlewares/GlobalExceptionMiddleware.cs**

```csharp
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware>  logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
       ...
        catch (Exception ex)
        {
            // 添加对未知异常的日志记录
            logger.LogError(
                ex,
                "Unhandled exception. Path={Path}, TraceId={TraceId}",
                context.Request.Path,
                context.TraceIdentifier);

            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "Unexpected error.");
        }
    }

   ...
}
```

修改之后，日志功能基本完整了：

- 业务流程日志：在 Service 里
- 未知异常日志：在中间件里
- 两边都能带 `TraceId`



### 5. 优化 - 使用BeginScope

上面那种写法虽然已经能用，但还有一个小问题：

- 每一条日志都要手动写 `TraceId={TraceId}`、`OrderId={OrderId}`

可以使用一个常见小工具：

```csharp
logger.BeginScope(...)
```

它的作用可以简单理解成：

> **给一段日志统一加上一个上下文标签。**

#### 5.1 为什么要用 BeginScope？

假设 Pay 这个流程里有 4~5 条日志。

如果每一条都手动写 `OrderId`，会很重复。

而 `BeginScope` 可以把 `OrderId` 这一类“这段流程共享的信息”统一挂上去。

#### 5.2 在 PayAsync 中使用 BeginScope

```csharp
public async Task<Order> PayAsync(int id, CancellationToken cancellationToken)
{
    var traceId = httpContextAccessor.HttpContext?.TraceIdentifier;

    using var _ = logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = id,
        ["TraceId"] = traceId ?? string.Empty
    });

    logger.LogInformation("Pay started.");

    var order = await repo.GetByIdAsync(id, cancellationToken);
    if (order is null)
    {
        logger.LogWarning("Pay failed: order not found.");
        throw new OrderNotFoundException(id);
    }

    if (order.Status == "Paid")
    {
        logger.LogWarning("Pay failed: order already paid.");
        throw new OrderConflictException($"Order {id} was already paid");
    }

    await repo.UpdateStatusAsync(id, "Paid", cancellationToken);

    logger.LogInformation("Pay succeeded.");

    return order;
}
```

注意：

**默认情况下，控制台输出可能看不见 Scope 的效果。必须在 `appsettings.json` 中明确开启它：**

```c#
{
  "Logging": {
    "Console": {
      "IncludeScopes": true
    }
  }
}
```

这样这段作用域里的日志就都共享：

- `OrderId`
- `TraceId`

可以把它理解成：

> **不用每条日志都重复写公共字段，而是给这一段流程统一贴标签。**



### 6. 优化 - 让日志自动带请求 Trace 信息

上面自定义方式的优点是：

**原理清楚、能明确看到 traceId 是怎么从 HttpContext 进入日志的。**

但到了较新的 ASP.NET Core 项目里，更推荐的做法是：

**让日志系统自己把请求 Trace 信息带进日志。**

也就是说：

- 不再手动从 `IHttpContextAccessor` 里拿 traceId
- 而是让日志框架自动把 TraceId / SpanId 放进日志上下文

#### 6.1 先明确：这一步解决什么问题？

它解决的是：

- 不想每个 Service 都手动读取 `HttpContext.TraceIdentifier`
- 不想每一条日志都手动写 `TraceId={TraceId}`
- 希望框架自动把请求级追踪信息带上

为了能更好的利用框架自带的组件，我们对异常响应的处理也使用框架字段的组件 `AddProblemDetails`和`UseExceptionHandler`

注册

```c#
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<OrderExceptionHandler>();
```

使用

```c#
app.UseExceptionHandler();
```

#### 6.2 Program.cs 配置日志输出作用域和 Trace 信息

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        
        // 异常处理的组件
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<OrderExceptionHandler>();
        
        // 日志系统的配置
        //配置日志输出作用域和 Trace 信息
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
        });

        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });
        

        builder.Services.AddScoped<OrderService>();
        builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
        builder.Services.AddDbContext<AppDbContext>(options =>options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddOpenApi(); 

        var app = builder.Build();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi(); 
            app.MapScalarApiReference(options => 
            {
                options.WithTitle("WebAPI_Basics Documentation")
                    .WithTheme(ScalarTheme.Moon); 
            });
        }
        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.UseExceptionHandler();
        
        app.MapControllers();
        app.Run();
    }
}
```

#### 6.3 原理是什么？

不再自己去 `httpContextAccessor.HttpContext?.TraceIdentifier`。

而是：

- 框架维护请求的 Trace 上下文
- 日志系统把这些 Trace 信息自动放进日志作用域
- 只要开启 `IncludeScopes`
- 日志输出里就能看到 TraceId / SpanId 等字段

所以这一版更像是：

> **把“请求级标识”交给框架自动处理。**

#### 6.4 这时 Service 层代码可以更干净

```csharp
using Microsoft.Extensions.Logging;
using WebAPI_Basics.Domain;
using WebAPI_Basics.Repositories;

namespace WebAPI_Basics.Services;

public class OrderService(
    IOrderRepository repo,
    ILogger<OrderService> logger)
{
    public async Task<Order> PayAsync(int id, CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["OrderId"] = id
        });

        logger.LogInformation("Pay started.");

        var order = await repo.GetByIdAsync(id, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Pay failed: order not found.");
            throw new OrderNotFoundException(id);
        }

        if (order.Status == "Paid")
        {
            logger.LogWarning("Pay failed: order already paid.");
            throw new OrderConflictException($"Order {id} was already paid");
        }

        await repo.UpdateStatusAsync(id, "Paid", cancellationToken);

        logger.LogInformation("Pay succeeded.");

        return order;
    }
}
```

这时：

- `OrderId` 仍然由你自己补（业务级上下文）
- `TraceId` 交给框架自动补（请求级上下文）

这就是更推荐的职责分工。



### 7. 两种方式怎么理解？

#### 方式一：自定义方式

- 手动从 `HttpContext.TraceIdentifier` 读取 traceId

- traceId的格式是：`0HNJQCR0TPI83:00000005` (HttpContext.TraceIdentifier)，

    是 ASP.NET Core **内部服务器（Kestrel）** 生成的一个简单字符串 ID。

    它主要用于在本地日志中跟踪 Web 服务器内部的请求处理过程。

    格式比较随意，通常是连接 ID + 自增序号。

- 手动把 traceId 写进日志

- 优点：原理最直观，最容易理解

#### 方式二：框架推荐方式

- 框架自动把 Trace 信息放进日志上下文

- Trace 信息可以包含 更多，比如 `SpanId`, `ParentId`

- traceId的格式是：`00-d771fc...-00` (Activity.Current.Id)

    是 **分布式追踪（W3C Trace Context）** 标准的 ID。

    **`.NET 7/8+` 的 `AddProblemDetails` 默认优先使用这个。**

    结构是：`版本号-追踪ID-父跨度ID-标志`。

    设计目的是为了在**微服务**之间跳转时，所有的服务都能共享同一个全局追踪 ID。

- 只负责业务级信息（例如 `OrderId`）

- 优点：代码更干净、更标准



### 8. 如何验证？

#### 验证1：Pay 成功

- 创建订单
- 调用 `POST /orders/{id}/pay`

预期控制台看到：

- `Pay started`
- `Pay succeeded`

并且日志里有：

- `OrderId`
- `TraceId`（自定义方式是手动写出来，推荐方式是 scope 自动带出来）

#### 验证2：订单不存在

- 调 `POST /orders/999999/pay`

预期：

- 日志里看到 `Pay failed: order not found`
- 日志里有 `TraceId`
- 客户端收到 404 ProblemDetails

这时客户端把 `traceId` 发给服务端，服务端可以去日志里搜这个值。

#### 验证3：重复支付

- 同一订单支付两次

预期：

- 第二次日志里看到 `Pay failed: order already paid`
- 客户端收到 409 ProblemDetails

#### 验证4：未知异常

例如暂时在 Repository 里故意抛一个 `new Exception("boom")`

预期：

- 中间件 `LogError(...)` 记录异常
- 日志里有 `TraceId`
- 客户端收到 500 ProblemDetails



### 9. 本章小结

本章真正解决的是：

> **让服务端“可排查”。**

两层能力：

#### 1）基础日志能力

- 在 Service 关键流程打 `Information / Warning`
- 在未知异常处打 `Error`

#### 2）traceId 真正打通

- 客户端错误响应里拿到 `traceId`
- 服务端日志里记录同一个 `traceId`
- 这样客户端报错时，后端才能按 `traceId` 查日志

#### 有什么新的问题？

现在日志已经能打出来了，但很快会发现：

- 日志级别是不是要按环境切换？
- 默认分页大小、最大页大小是不是还写死在代码里？
- 数据库连接字符串是不是也写死了？
- JWT 密钥后面肯定也不能写死

所以要解决的是：

> **配置管理：appsettings + Options**

也就是：把那些“会变化的参数”从代码里抽出去，放到配置里统一管理。



下面我把第10章**重新生成**成更“顺滑”的学习笔记：
**每个概念都必须有前因后果**——先出现问题 → 解释为什么会这样 → 引出概念 → 再落到代码与验证。不会突然冒出一个新名词就开始写代码。



## 10. 配置管理

### 1. 解决什么问题？

到第9章为止，我们的项目已经具备“真实项目的骨架”：数据库、异常收口、ProblemDetails、日志。

但现在代码里还有很多**写死（hard-code）**的东西，比如：

- 数据库连接字符串（换环境就要改代码/容易泄露）
- 分页默认值、最大 PageSize（产品改规则就要改代码再发版）
- 日志级别（开发想看多一点，生产想更克制

因此，本章要解决的核心问题：

**把“会变的参数”从代码里拿出去，用配置管理。**



### 2. 什么是配置？

配置就是程序运行时需要的一些“外部参数”：

- 这些参数会变（按环境、按需求、按开关）
- 但代码不应该跟着频繁变

所以配置的目标是：

**让程序“同一份代码”，在不同环境/不同规则下运行。**

**ASP.NET Core 从哪里读配置？**

最常见来源是两个 JSON 文件：

- `appsettings.json`：默认配置（所有环境的“基线值”）
- `appsettings.Development.json`：开发环境覆盖默认值（只在 Development 环境生效）

一个重要的规则：

- **更具体的配置会覆盖更通用的配置。**

    比如： Development 文件会覆盖 appsettings.json 里的同名项。

这就是为什么要有两个文件：

默认值放基线，开发差异放 Development 覆盖。



### 3. 如何读取配置？

现在我们知道，配置时要把一些参数，比如 `DefaultPageSize / MaxPageSize` ，放到 appsettings。

那么，使用时，**业务代码里怎么读配置？**

框架已经内置了配置对象 `Builder.Configration`。 

使用时，通过IConfiguration接口直接注入并拿到配置对象

比如：

```c#
public class EfOrderRepository(AppDbContext db, IConfiguration config) : IOrderRepository
{
    // config就是全局配置对象
}
```

读取配置参数时，直接使用配置对象内置的方法 `GetValue<T>`

```c#
public class EfOrderRepository(AppDbContext db, IConfiguration config) : IOrderRepository
{
    var defaultSize = config.GetValue<int>("OrderApi:DefaultPageSize");
    var maxSize = config.GetValue<int>("OrderApi:MaxPageSize");
    ...
}
```

还有最原始的读法：用字符串 key：

```csharp
var defaultSize = config["OrderApi:DefaultPageSize"];
var maxSize = config["OrderApi:MaxPageSize"];
```

这有明显缺点：

- key 是字符串，写错不会报错，只会读不到
- 值是 string，你还要自己转 int
- 读配置的代码容易散落在各处（维护更难）

因此需要对配置对象参数也要统一封装管理， 这就是**Options 模式**。



### 4. Options 模式

Options 模式：**把一组相关配置，绑定成一个强类型对象，然后通过 DI 注入使用。**

它能解决上面的几个缺点：

- 不再到处写字符串 key（集中在绑定那一处）
- 不再手动转类型（int 就是 int）
- 配置读取集中、结构清晰（像注入 Service 一样注入配置）

也就说，把配置对象中的参数，也封装成一个类，这个类可以绑定到配置appsettings.json对应的实例中，使用时通过DI注入的方式。

这样就能实现对配置参数的封装和统一管理。



### 5. 代码实现

本章只做一件最典型的配置化：**分页默认值/最大值**。

数据库的连接字符串之前已经放 appsettings 里了，并通过 `Builder.Configration`获取，并传入AddDbContext

```c#
// 注册数据库
builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

#### 5.1 把参数写进 appsettings

**appsettings.json（基线值）**

```json
{
...
   
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=WebApiBasicsDb;User Id=sa;Password=MYpwd123!;TrustServerCertificate=True;Encrypt=False"
  },
    
  "OrderApi": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}

```

**appsettings.Development.json（开发覆盖，可选）**

```json
{
  "OrderApi": {
    "DefaultPageSize": 10,
    "MaxPageSize": 200
  }
}
```

到这里只做了“把参数外移”，但还没让代码用上。

#### 5.2 创建一个强类型 Options 类

让配置参数“有类型”。

**Options/OrderApiOptions.cs**

```csharp
namespace WebAPI_Basics.Options;

public sealed class OrderApiOptions
{
    public int DefaultPageSize { get; init; } = 20;
    public int MaxPageSize { get; init; } = 100;
}
```

这一步使得：配置不再是字符串字典，而是一个“有字段、有类型”的对象

#### 5.3 Program.cs 绑定配置

把`appsettings.json`里的参数（字符串字典），绑定到Options 类中

**Program.cs**

```csharp
using WebAPI_Basics.Options;

builder.Services.Configure<OrderApiOptions>(
    builder.Configuration.GetSection("OrderApi"));
```

这一步非常关键，它完成了：

> ```
> OrderApi:*` 这组配置 → 绑定到 `OrderApiOptions
> ```

绑定完成后，DI 就可以提供：

- `IOptions<OrderApiOptions>`

#### 5.4 使用配置参数

在需要的地方注入 IOptions，使用Options类的实例使用

**Repositories/EfOrderRepository.cs**

```csharp
using Microsoft.Extensions.Options;
using WebAPI_Basics.Options;

public class EfOrderRepository(AppDbContext db, IOptions<OrderApiOptions> options) : IOrderRepository
{
    private readonly OrderApiOptions _opt = options.Value;

    public async Task<List<Order>> GetAllAsync(OrderQueryRequest query, CancellationToken ct)
    {
        IQueryable<Order> result = db.Orders;

        // ... 过滤/排序保持不变
        
		// 3) 分页
        var page = query.Page < 1 ? 1 : query.Page;

        var pageSize = query.PageSize < 1
            ? _opt.DefaultPageSize
            : query.PageSize;

        pageSize = Math.Min(pageSize, _opt.MaxPageSize);

        result = result.Skip((page - 1) * pageSize).Take(pageSize);

        return await result.ToListAsync(ct);
    }
}
```

到这里，本章目标就达成了：

- 业务逻辑完全没变（分页规则不变）
- 只是把“数字”从代码移到了配置



### 6. 如何验证

#### 验证1：改配置，不改代码，默认分页大小就变

1. 把 `appsettings.Development.json` 的 `DefaultPageSize` 改成 5
2. 请求 `GET /orders?page=1`（不传 pageSize）
3. 预期：最多返回 5 条

#### 验证2：最大 pageSize 上限受配置控制

1. 把 `MaxPageSize` 改成 30
2. 请求 `GET /orders?page=1&pageSize=9999`
3. 预期：最多返回 30 条

#### 验证3：Development 覆盖生效

- 你在 Development 下跑，应该使用 Development 文件的值覆盖基线值



### 7. 本章小结

本章完成了“真实项目必备”的配置化能力：

- 配置文件（appsettings / Development 覆盖）
- 配置绑定（Options 模式）
- 用 DI 注入配置
- 用配置替换硬编码（分页默认值/最大值）

**有什么新的问题？**

api现在具体最基础的配置能力，但是遇到更多配置时，如何处理？

比如：

**如何做一个最小可用的 JWT 认证（登录签 Token + 保护接口），并把 JWT 参数配置化？**



## 11. JWT 认证

### 1. 解决什么问题？

现在任何人只要知道 API 地址，就能调用：

- 创建订单
- 支付订单
- 查询订单

在真实项目里，这样是不可接受的，至少要做到：

> **写操作必须登录（才能 pay / create），否则直接拒绝。**

所以本章要解决的就是：

1. **认证（Authentication）**：你是谁？有没有登录？
2. **授权（Authorization）**：你有没有权限做这件事？

JWT 是 API 项目里最常见的入门方案。



### 2. 什么是JWT？

#### Token 是什么？

Token 是服务器发给客户端的一串字符串。客户端以后每次请求都带上它：

```
Authorization: Bearer <token>
```

当用户登录成功后，服务器会签发给用户一张“通行证”，之后用户访问其他受保护的接口时，只需出示这张证件，服务器无需再次查询数

据库即可验证用户的身份。

JWT (JSON Web Token) 一种**有特定格式、自带数据**的 Token。

#### JWT 的三段结构

JWT 长这样：

```
header.payload.signature
```

- header： 描述令牌的元数据（如使用的加密算法，通常是 HMAC SHA256）。

- payload：放 claims（如用户 ID、角色、过期时间等），存放实际数据的地方。

- signature：签名（防伪）。由 Header、Payload 和服务器的一个**秘密密钥（Secret Key）**共同加密生成。**如果有人修改了 Payload，签名就会失效。**

关键点：**JWT 默认不是加密的，是签名的。**

也就是说 payload 理论上可以被别人 base64 解开看内容，所以不要把密码等敏感信息放进 claims。

#### Claims 是什么？

JWT 的 Payload（载荷）部分是一个 JSON 对象。JWT 标准（RFC 7519）定义了一套**注册声明（Registered Claims）**。

| **键**    | **名称**   | **说明**                                                     |
| --------- | ---------- | ------------------------------------------------------------ |
| **`iss`** | Issuer     | **签发者**。表明这个 Token 是由哪个服务生成的（如 `auth.myapp.com`）。 |
| **`sub`** | Subject    | **主题/主体**。通常存放唯一用户标识（如 `user_id`）。        |
| **`aud`** | Audience   | **接收者**。表明这个 Token 是给哪个服务使用的（如 `api.myapp.com`）。 |
| **`exp`** | Expiration | **过期时间**。必须是 Unix 时间戳（秒），过期后 Token 无效。  |
| **`nbf`** | Not Before | **生效时间**。在此时间之前，Token 也是无效的。               |
| **`iat`** | Issued At  | **签发时间**。记录 Token 是什么时候创建的。                  |
| **`jti`** | JWT ID     | **唯一标识**。用于防止重放攻击（像一次性入场券的编号）。     |

token 里携带的一些“身份标签”，例如：

- username
- userId
- role

会被放到 `HttpContext.User` 里, 后面授权会用。

#### Secret Key是什么？

服务器用它生成签名，验证时也用它。

**密钥泄露 = 任何人都能伪造 token**，所以必须配置化。



### 3. 实现路线

在现有“Order API（数据库 + 全局异常 + ProblemDetails + 日志 + 配置）”基础上，**最小改动**加上 JWT：

- 先能“登录拿 token”，再能“带 token 访问受保护接口”。
- 不做注册、不做用户表、不做刷新令牌。

1. **配置 JwtOptions**
2. **注册 JWT Bearer 认证**（让服务器能“验证 token”）
3. **写一个最小 login 接口**（签发 token）
4. **给一个接口加 [Authorize]**（验证保护生效）
5. **用 Scalar 带 token 调用受保护接口**



### 4. 配置 JWT bearer authentication

#### 4.1 配置 JwtOptions

1. appsettings.json 增加 Jwt 节

    ```c#
    {
      "Jwt": {
        "Issuer": "WebApiBasics",
        "Audience": "WebApiBasicsClient",
        "Key": "PLEASE_CHANGE_TO_A_LONG_RANDOM_SECRET_KEY_32+_CHARS",
        "ExpireMinutes": 60
      }
    }
    ```

    **这四个字段什么意思？**

    - Issuer：签发者（谁发的 token）
    - Audience：接收者（token 给谁用）
    - Key：签名密钥（最重要，不能短）
    - ExpireMinutes：有效期

    为什么要有 Issuer/Audience？

    因为 token 不是只看签名，也要验证“是不是我这个系统发的，给我这个系统用的”。

2. 创建Options 类：JwtOptions

    Options/JwtOptions.cs

    ```c#
    public sealed class JwtOptions
    {
        public string Issuer { get; init; } = "";
        public string Audience { get; init; } = "";
        public string Key { get; init; } = "";
        public int ExpireMinutes { get; init; } = 60;
    }
    ```

3. Program.cs 绑定配置

    ```c#
    builder.Services.Configure<JwtOptions>(
        builder.Configuration.GetSection("Jwt"));
    ```

    到这里，配置已经“收口”成一个强类型对象了。

#### 4.2 注册 JWT Bearer

这一部分解决的问题是：

> 客户端以后带 `Authorization: Bearer xxx` 来了，服务器怎么解析、怎么验证？

1. 添加 NuGet

    一般 Web API 模板会有，但如果缺少，需要：

    ```c#
    Microsoft.AspNetCore.Authentication.JwtBearer
    ```

2. Program.cs 添加认证与授权注册，并配置认证规则

    ```c#
    
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer((options) =>
        {
            // 从配置系统里取出jwt，再“反序列化”一个 JwtOptions 对象（不是通过 DI）
            // 因为配置阶段无法拿到通过DI拿到的对象，DI是在app.run()之后才执行
            var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
                      ?? throw new Exception("Jwt config section missing");
            if(string.IsNullOrEmpty(jwt.Key))
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
    
    builder.Services.AddAuthorization();
    ```

    **这段代码在做什么？**

    - `AddAuthentication(...)`：告诉框架“我们用 JWT Bearer 作为认证方式”
    - `AddJwtBearer(...)`：配置怎么验证 token（issuer/audience/key/expire）
    - `AddAuthorization()`：允许使用 `[Authorize]` 等授权机制

3. 启用中间件（非常重要的顺序）

    ```c#
    app.UseAuthentication();
    app.UseAuthorization();
    ```

    必须放在 `MapControllers()` 前面。

    **为什么要两句？**

    - `UseAuthentication()`：把 token 解析成 `HttpContext.User`
    - `UseAuthorization()`：检查 `[Authorize]`，决定让不让进 Action

4. 升级配置认证规则 - 使用扩展方法

    Program.cs 里 JWT 配置规则太多， 通常会把“基础设施配置”收口到单独层里，让 Program.cs 干净。

    最常见的方式是**使用扩展方法收口**

    新建扩展方法类，并编写扩展方法 AddJwtAuth

    `Extensions/ServiceCollectionExtensions.cs`

    ```c#
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
    ```

    主程序中使用扩展方法

    ```c#
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
    
            // 异常处理的组件
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<OrderExceptionHandler>();
            
            //配置日志输出 Trace 信息
            builder.Logging.Configure(options =>
            {
                options.ActivityTrackingOptions =
                    ActivityTrackingOptions.TraceId |
                    ActivityTrackingOptions.SpanId |
                    ActivityTrackingOptions.ParentId;
            });
    
            // 注册配置对象OrderApiOptions，并绑定配置参数OrderApi
            builder.Services.Configure<OrderApiOptions>(builder.Configuration.GetSection("OrderApi"));
            // 注册配置对象JwtOptions，并绑定配置参数Jwt
            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
            
            
           // 使用扩展方法，实现注册Jwt认证。
            builder.Services.AddJwtAuth(builder.Configuration);
       		// 注册授权服务
            builder.Services.AddAuthorization();
    
            builder.Services.AddScoped<OrderService>();
            builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddOpenApi();
    
            var app = builder.Build();
                
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.WithTitle("WebAPI_Basics Documentation")
                        .WithTheme(ScalarTheme.Moon);
                });
            }
            app.UseHttpsRedirection();
            
            // 认证，鉴别身份
            app.UseAuthentication();
            // 启用授权中间件（Authorization）
            app.UseAuthorization();
            
            app.UseExceptionHandler();
            app.MapControllers();
    
            app.Run();
        }
    }
    ```

    

### 5. 登录接口

现在服务器“会验证 token”了，但客户端还没有 token。

所以我们需要一个 `/auth/login`，返回 token。

> 注意：这只是演示。真实项目会查数据库用户、密码哈希、刷新令牌等。
>
> 本章只做“最小闭环”。

#### 5.1 创建LoginRequest DTO

**Dtos/Requests/LoginRequest.cs**

```csharp
public sealed class LoginRequest
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
}
```

#### 5.2 创建AuthController 

用来签发 token

**Controllers/AuthController.cs**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebAPI_Basics.Options;

[ApiController]
[Route("auth")]
public sealed class AuthController(IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(LoginRequest req)
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
```

**这一段要看懂的“机制”只有三步：**

1. 准备 claims
2. 用 key + algorithm 签名
3. 生成 token 并返回字符串



#### 5.3. 保护接口（[Authorize]）

我们只先保护一个接口（例如 Pay）。

**OrdersController.cs**

```csharp
using Microsoft.AspNetCore.Authorization;

[Authorize]
[HttpPost("{id:int}/pay")]
public async Task<IActionResult> Pay(int id, CancellationToken ct)
{
    var order = await orderService.PayAsync(id, ct);
    return Ok(order);
}
```

**会发生什么？**

- 没 token：直接 401（甚至进不了 Action）
- token 合法：进入 Action
- token 无效/过期：401



### 6. 用 Scalar 验证

#### 6.1 拿 token

调用：

- `POST /auth/login`
    body：

```json
{ "username": "admin", "password": "123456" }
```

拿到 `access_token`

#### 6.2 带 token 调用受保护接口

在请求 header 加：

```
Authorization: Bearer <eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiYWRtaW4iLCJyb2xlIjoiYWRtaW4iLCJleHAiOjE3NzMzMjA5MTYsImlzcyI6IldlYkFwaUJhc2ljcyIsImF1ZCI6IldlYkFwaUJhc2ljc0NsaWVudCJ9.69WS7A2ecl3Y-FUjhVkunSSSGd9DQea29YoKxTya5k0>
```

调用 `POST /orders/{id}/pay`

#### 6.3 预期现象

- 不带 token：401
- 带正确 token：200
- 带乱 token：401



### 7. 本章小结

本章完成了 JWT 的最小闭环：

- 配置化 JwtOptions（复用第10章）
- 注册 JwtBearer 验证（服务器能验 token）
- login 签发 token（客户端能拿 token）
- [Authorize] 保护接口（安全生效）
- Scalar 带 Bearer token 调试

**有什么新的问题？**

现在加了认证授权，回归风险更大：

一改代码就可能导致 login 失效、pay 被误开放、或错误返回不一致。

因此，需要用测试锁住高价值场景（Pay 成功 / 重复支付 / 不存在订单）。


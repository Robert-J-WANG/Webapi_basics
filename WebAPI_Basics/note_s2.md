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
